using System.Data;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessLogic.Settings;
using BusinessObject.Entities;
using DataAccess;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessLogic.Services.Implements;

public sealed class PaymentService : IPaymentService
{
    private readonly IPaymentTransactionRepository _paymentRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly IWalletRepository _walletRepo;
    private readonly SchoolManagementDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MoMoSettings _momoSettings;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentTransactionRepository paymentRepo,
        IStudentRepository studentRepo,
        IWalletRepository walletRepo,
        SchoolManagementDbContext context,
        IHttpClientFactory httpClientFactory,
        IOptions<MoMoSettings> momoSettings,
        ILogger<PaymentService> logger)
    {
        _paymentRepo = paymentRepo;
        _studentRepo = studentRepo;
        _walletRepo = walletRepo;
        _context = context;
        _httpClientFactory = httpClientFactory;
        _momoSettings = momoSettings.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<MoMoCreatePaymentResponse>> CreateDepositAsync(int userId, decimal amount)
    {
        _logger.LogInformation("CreateDepositAsync started — UserId={UserId}", userId);

        if (amount <= 0)
        {
            _logger.LogWarning("CreateDepositAsync INVALID_AMOUNT — UserId={UserId}", userId);
            return ServiceResult<MoMoCreatePaymentResponse>.Fail("INVALID_AMOUNT", "Số tiền nạp phải lớn hơn 0.");
        }

        var student = await _studentRepo.GetStudentByUserIdAsync(userId);
        if (student == null)
        {
            _logger.LogWarning("CreateDepositAsync STUDENT_NOT_FOUND — UserId={UserId}", userId);
            return ServiceResult<MoMoCreatePaymentResponse>.Fail("STUDENT_NOT_FOUND", "Không tìm thấy sinh viên.");
        }

        var requestId = Guid.NewGuid().ToString("N");
        var orderId = Guid.NewGuid().ToString("N");
        var orderInfo = $"Nạp ví sinh viên {student.StudentCode}";
        var momoAmount = (long)amount;
        var extraData = string.Empty;

        var rawSignature = $"accessKey={_momoSettings.AccessKey}" +
                           $"&amount={momoAmount}" +
                           $"&extraData={extraData}" +
                           $"&ipnUrl={_momoSettings.NotifyUrl}" +
                           $"&orderId={orderId}" +
                           $"&orderInfo={orderInfo}" +
                           $"&partnerCode={_momoSettings.PartnerCode}" +
                           $"&redirectUrl={_momoSettings.ReturnUrl}" +
                           $"&requestId={requestId}" +
                           $"&requestType={_momoSettings.RequestType}";

        var signature = ComputeHmacSha256(rawSignature, _momoSettings.SecretKey);

        var momoRequest = new
        {
            partnerCode = _momoSettings.PartnerCode,
            requestId,
            amount = momoAmount,
            orderId,
            orderInfo,
            redirectUrl = _momoSettings.ReturnUrl,
            ipnUrl = _momoSettings.NotifyUrl,
            requestType = _momoSettings.RequestType,
            extraData,
            lang = "vi",
            signature
        };

        var paymentTransaction = new PaymentTransaction
        {
            StudentId = student.StudentId,
            PaymentMethod = "MOMO",
            MoMoRequestId = requestId,
            MoMoOrderId = orderId,
            Amount = amount,
            OrderInfo = orderInfo,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        await _paymentRepo.CreateAsync(paymentTransaction);
        _logger.LogInformation("CreateDepositAsync PaymentTransaction created — TransactionId={TransactionId}, OrderId={OrderId}", paymentTransaction.TransactionId, orderId);

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(_momoSettings.Endpoint, momoRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            using var jsonDoc = JsonDocument.Parse(responseBody);
            var root = jsonDoc.RootElement;

            var resultCode = root.GetProperty("resultCode").GetInt32();

            if (resultCode != 0)
            {
                var momoMessage = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown";
                _logger.LogWarning("CreateDepositAsync MoMo rejected — OrderId={OrderId}, ResultCode={ResultCode}, Message={Message}", orderId, resultCode, momoMessage);
                return ServiceResult<MoMoCreatePaymentResponse>.Fail("MOMO_ERROR", $"MoMo trả về lỗi: {momoMessage}");
            }

            var payUrl = root.GetProperty("payUrl").GetString()!;

            _logger.LogInformation("CreateDepositAsync SUCCESS — OrderId={OrderId}, TransactionId={TransactionId}", orderId, paymentTransaction.TransactionId);

            return ServiceResult<MoMoCreatePaymentResponse>.Success(new MoMoCreatePaymentResponse
            {
                PayUrl = payUrl,
                OrderId = orderId,
                RequestId = requestId,
                TransactionId = paymentTransaction.TransactionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateDepositAsync MoMo HTTP call failed — OrderId={OrderId}", orderId);
            return ServiceResult<MoMoCreatePaymentResponse>.Fail("MOMO_CONNECTION_ERROR", "Không thể kết nối đến MoMo, vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult> HandleMoMoCallbackAsync(MoMoCallbackRequest payload)
    {
        _logger.LogInformation("HandleMoMoCallbackAsync started — OrderId={OrderId}, ResultCode={ResultCode}", payload.OrderId, payload.ResultCode);

        var expectedSignature = ComputeCallbackSignature(payload);
        if (!string.Equals(expectedSignature, payload.Signature, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("HandleMoMoCallbackAsync INVALID_SIGNATURE — OrderId={OrderId}", payload.OrderId);
            return ServiceResult.Fail("INVALID_SIGNATURE", "Chữ ký không hợp lệ.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var paymentTx = await _paymentRepo.GetByMoMoOrderIdAsync(payload.OrderId);
            if (paymentTx == null)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("HandleMoMoCallbackAsync TRANSACTION_NOT_FOUND — OrderId={OrderId}", payload.OrderId);
                return ServiceResult.Fail("TRANSACTION_NOT_FOUND", "Không tìm thấy giao dịch.");
            }

            if (string.Equals(paymentTx.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync();
                _logger.LogInformation("HandleMoMoCallbackAsync IDEMPOTENT — OrderId={OrderId}, already SUCCESS", payload.OrderId);
                return ServiceResult.Success("Giao dịch đã được xử lý trước đó.");
            }

            paymentTx.MoMoTransId = payload.TransId;
            paymentTx.ErrorCode = payload.ResultCode;
            paymentTx.LocalMessage = payload.Message;
            paymentTx.PaymentDate = DateTime.UtcNow;

            if (payload.ResultCode == 0)
            {
                paymentTx.Status = "SUCCESS";

                var wallet = await _walletRepo.GetWalletForUpdateAsync(paymentTx.StudentId);
                if (wallet == null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("HandleMoMoCallbackAsync Wallet not found — StudentId={StudentId}, OrderId={OrderId}", paymentTx.StudentId, payload.OrderId);
                    return ServiceResult.Fail("WALLET_NOT_FOUND", "Ví sinh viên không tồn tại.");
                }

                wallet.Balance += paymentTx.Amount;
                wallet.LastUpdated = DateTime.UtcNow;

                var walletTx = new WalletTransaction
                {
                    WalletId = wallet.WalletId,
                    Amount = paymentTx.Amount,
                    TransactionType = "DEPOSIT",
                    RelatedPaymentId = paymentTx.TransactionId,
                    Description = $"Nạp ví qua MoMo — OrderId: {payload.OrderId}",
                    CreatedAt = DateTime.UtcNow
                };

                await _walletRepo.AddWalletTransactionAsync(walletTx);
                await _paymentRepo.UpdateAsync(paymentTx);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("HandleMoMoCallbackAsync DEPOSIT SUCCESS — OrderId={OrderId}, StudentId={StudentId}, TransactionId={TransactionId}", payload.OrderId, paymentTx.StudentId, paymentTx.TransactionId);
                return ServiceResult.Success("Nạp ví thành công.");
            }
            else
            {
                paymentTx.Status = payload.ResultCode == 1006 ? "CANCELLED" : "FAILED";

                await _paymentRepo.UpdateAsync(paymentTx);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogWarning("HandleMoMoCallbackAsync PAYMENT FAILED — OrderId={OrderId}, ResultCode={ResultCode}, Status={Status}", payload.OrderId, payload.ResultCode, paymentTx.Status);
                return ServiceResult.Fail("PAYMENT_FAILED", $"Thanh toán thất bại: {payload.Message}");
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "HandleMoMoCallbackAsync EXCEPTION — OrderId={OrderId}", payload.OrderId);
            return ServiceResult.Fail("SYSTEM_ERROR", "Có lỗi hệ thống, vui lòng thử lại.");
        }
    }

    private string ComputeCallbackSignature(MoMoCallbackRequest payload)
    {
        var rawSignature = $"accessKey={_momoSettings.AccessKey}" +
                           $"&amount={payload.Amount}" +
                           $"&extraData={payload.ExtraData}" +
                           $"&message={payload.Message}" +
                           $"&orderId={payload.OrderId}" +
                           $"&orderInfo={payload.OrderInfo}" +
                           $"&orderType={payload.OrderType}" +
                           $"&partnerCode={payload.PartnerCode}" +
                           $"&payType={payload.PayType}" +
                           $"&requestId={payload.RequestId}" +
                           $"&responseTime={payload.ResponseTime}" +
                           $"&resultCode={payload.ResultCode}" +
                           $"&transId={payload.TransId}";

        return ComputeHmacSha256(rawSignature, _momoSettings.SecretKey);
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
