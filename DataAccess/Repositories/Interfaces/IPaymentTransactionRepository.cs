using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IPaymentTransactionRepository
{
    Task<PaymentTransaction?> GetByMoMoOrderIdAsync(string orderId);
    Task<PaymentTransaction?> GetByIdAsync(long id);
    Task CreateAsync(PaymentTransaction entity);
    Task UpdateAsync(PaymentTransaction entity);
}
