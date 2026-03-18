(function () {
    'use strict';

    let currentSessionId = null;
    let isSending = false;
    const MAX_NETWORK_RETRIES = 1;
    const NETWORK_RETRY_DELAY_MS = 1500;

    const purposeSelect = document.getElementById('purposeSelect');
    const newSessionBtn = document.getElementById('aiNewSessionBtn');
    const messageList = document.getElementById('aiMessageList');
    const messageInput = document.getElementById('aiMessageInput');
    const sendBtn = document.getElementById('aiSendBtn');
    const loadingIndicator = document.getElementById('aiLoadingIndicator');
    const alertArea = document.getElementById('aiAlertArea');
    const alertContent = document.getElementById('aiAlertContent');
    const emptyState = document.getElementById('aiEmptyState');
    const chatContainer = document.getElementById('aiChatContainer');
    const sessionInfoCard = document.getElementById('sessionInfoCard');
    const sessionInfoText = document.getElementById('sessionInfoText');

    function getAntiForgeryToken() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function showAlert(message, isError, isRateLimit) {
        alertContent.textContent = message;
        if (isRateLimit) {
            alertContent.className = 'ai-alert ai-alert-rate-limit';
        } else {
            alertContent.className = isError ? 'ai-alert ai-alert-error' : 'ai-alert ai-alert-success';
        }
        alertArea.style.display = 'block';
        var timeout = isRateLimit ? 10000 : 6000;
        setTimeout(function () { alertArea.style.display = 'none'; }, timeout);
    }

    function hideAlert() {
        alertArea.style.display = 'none';
    }

    function setLoading(loading) {
        isSending = loading;
        sendBtn.disabled = loading || !messageInput.value.trim();
        loadingIndicator.style.display = loading ? 'flex' : 'none';
        messageInput.disabled = loading;
    }

    function scrollToBottom() {
        messageList.scrollTop = messageList.scrollHeight;
    }

    function showChat() {
        emptyState.style.display = 'none';
        chatContainer.style.display = 'flex';
    }

    function formatContent(content) {
        if (!content) return '';
        var escaped = escapeHtml(content);
        escaped = escaped.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
        escaped = escaped.replace(/\n/g, '<br>');
        return escaped;
    }

    // ===== Structured Data Rendering =====

    function renderStructuredData(data) {
        var html = '';

        // Summary Cards
        var cards = data.summaryCards;
        if (cards && cards.length > 0) {
            html += '<div class="ai-struct-section"><div class="ai-struct-title"><i class="fas fa-chart-bar"></i> Summary</div>';
            html += '<div class="ai-summary-grid">';
            for (var i = 0; i < cards.length; i++) {
                var c = cards[i];
                html += '<div class="ai-summary-card">';
                html += '<div class="ai-sc-label">' + escapeHtml(c.label || c.key || '') + '</div>';
                html += '<div class="ai-sc-value">' + escapeHtml(c.value || '') +
                    (c.unit ? ' <span class="ai-sc-unit">' + escapeHtml(c.unit) + '</span>' : '') + '</div>';
                html += '</div>';
            }
            html += '</div></div>';
        }

        // Risk Flags
        var risks = data.riskFlags;
        if (risks && risks.length > 0) {
            html += '<div class="ai-struct-section"><div class="ai-struct-title"><i class="fas fa-exclamation-triangle"></i> Risk Flags</div>';
            html += '<div class="ai-risk-list">';
            for (var j = 0; j < risks.length; j++) {
                var r = risks[j];
                var sev = (r.severity || 'LOW').toUpperCase();
                html += '<div class="ai-risk-item ai-risk-' + escapeHtml(sev.toLowerCase()) + '">';
                html += '<span class="ai-risk-badge">' + escapeHtml(sev) + '</span>';
                if (r.courseCode) html += '<strong>' + escapeHtml(r.courseCode) + '</strong> — ';
                html += escapeHtml(r.message || '');
                html += '</div>';
            }
            html += '</div></div>';
        }

        // Recommended Actions
        var actions = data.recommendedActions;
        if (actions && actions.length > 0) {
            html += '<div class="ai-struct-section"><div class="ai-struct-title"><i class="fas fa-tasks"></i> Recommended Actions</div>';
            html += '<div class="ai-actions-list">';
            var sorted = actions.slice().sort(function (a, b) { return (a.priority || 3) - (b.priority || 3); });
            for (var k = 0; k < sorted.length; k++) {
                var a = sorted[k];
                html += '<div class="ai-action-item">';
                html += '<span class="ai-action-priority">P' + (a.priority || 3) + '</span>';
                html += '<div class="ai-action-body">';
                html += '<div class="ai-action-title">' + escapeHtml(a.title || '') + '</div>';
                html += '<div class="ai-action-detail">' + escapeHtml(a.detail || '') + '</div>';
                html += '</div></div>';
            }
            html += '</div></div>';
        }

        // Plans
        var plans = data.plans;
        if (plans && plans.length > 0) {
            html += '<div class="ai-struct-section"><div class="ai-struct-title"><i class="fas fa-route"></i> Plans</div>';
            html += '<div class="ai-plans-list">';
            for (var p = 0; p < plans.length; p++) {
                var pl = plans[p];
                html += '<div class="ai-plan-card">';
                html += '<div class="ai-plan-header">';
                html += '<span class="ai-plan-name">' + escapeHtml(pl.planName || 'Plan ' + (p + 1)) + '</span>';
                html += '<span class="ai-plan-credits">' + (pl.totalCredits || 0) + ' credits</span>';
                if (typeof pl.constraintsOk === 'boolean') {
                    html += pl.constraintsOk
                        ? '<span class="ai-plan-ok"><i class="fas fa-check-circle"></i> OK</span>'
                        : '<span class="ai-plan-warn"><i class="fas fa-times-circle"></i> Issues</span>';
                }
                html += '</div>';
                if (pl.courseCodes && pl.courseCodes.length > 0) {
                    html += '<div class="ai-plan-courses">';
                    for (var ci = 0; ci < pl.courseCodes.length; ci++) {
                        html += '<span class="ai-plan-course-tag">' + escapeHtml(pl.courseCodes[ci]) + '</span>';
                    }
                    html += '</div>';
                }
                if (pl.notes && pl.notes.length > 0) {
                    html += '<ul class="ai-plan-notes">';
                    for (var ni = 0; ni < pl.notes.length; ni++) {
                        html += '<li>' + escapeHtml(pl.notes[ni]) + '</li>';
                    }
                    html += '</ul>';
                }
                html += '</div>';
            }
            html += '</div></div>';
        }

        // Disclaimer
        if (data.disclaimer) {
            html += '<div class="ai-struct-disclaimer"><i class="fas fa-info-circle"></i> ' + escapeHtml(data.disclaimer) + '</div>';
        }

        return html;
    }

    function appendMessage(senderType, content, warning, structuredData) {
        var bubble = document.createElement('div');
        bubble.className = senderType === 'USER' ? 'ai-bubble ai-bubble-user' : 'ai-bubble ai-bubble-assistant';

        var avatar = document.createElement('div');
        avatar.className = 'bubble-avatar';
        avatar.innerHTML = senderType === 'USER'
            ? '<i class="fas fa-user-graduate"></i>'
            : '<i class="fas fa-robot"></i>';

        var body = document.createElement('div');
        body.className = 'bubble-body';

        var label = document.createElement('div');
        label.className = 'bubble-label';
        label.textContent = senderType === 'USER' ? 'You' : 'AI Assistant';

        body.appendChild(label);

        // Structured data rendering for assistant messages
        if (senderType !== 'USER' && structuredData && typeof structuredData === 'object' && structuredData.purpose) {
            var structBlock = document.createElement('div');
            structBlock.className = 'bubble-structured';
            structBlock.innerHTML = renderStructuredData(structuredData);
            body.appendChild(structBlock);
        } else {
            var text = document.createElement('div');
            text.className = 'bubble-text';
            text.innerHTML = formatContent(content);
            body.appendChild(text);
        }

        if (warning) {
            var warn = document.createElement('div');
            warn.className = 'bubble-warning';
            warn.innerHTML = '<i class="fas fa-exclamation-triangle"></i> ' + escapeHtml(warning);
            body.appendChild(warn);
        }

        bubble.appendChild(avatar);
        bubble.appendChild(body);

        messageList.appendChild(bubble);
        scrollToBottom();
    }

    function updateSessionInfo(data) {
        if (!data) { sessionInfoCard.style.display = 'none'; return; }
        sessionInfoCard.style.display = 'block';
        var purpose = data.purpose || '';
        var state = data.state || '';
        sessionInfoText.textContent = 'Purpose: ' + purpose + ' | State: ' + state;
    }

    function isNetworkError(err) {
        return err instanceof TypeError && err.message.toLowerCase().includes('fetch');
    }

    function isBusinessError(errorCode) {
        var businessCodes = ['INVALID_INPUT', 'FORBIDDEN', 'SESSION_NOT_FOUND', 'INVALID_STATE', 'INVALID_TOOL', 'SAFE_GUARD_TRIGGERED', 'INVALID_AI_SCHEMA'];
        return businessCodes.indexOf(errorCode) !== -1;
    }

    async function fetchWithRetry(url, options, retries) {
        for (var i = 0; i <= retries; i++) {
            try {
                var response = await fetch(url, options);
                if (response.ok) return response;

                var json = null;
                try { json = await response.clone().json(); } catch (_) { }

                if (json && (isBusinessError(json.errorCode) || json.errorCode === 'RATE_LIMITED')) {
                    return response;
                }

                if (i < retries && response.status >= 500) {
                    await new Promise(function (r) { setTimeout(r, NETWORK_RETRY_DELAY_MS); });
                    continue;
                }

                return response;
            } catch (err) {
                if (i < retries && isNetworkError(err)) {
                    await new Promise(function (r) { setTimeout(r, NETWORK_RETRY_DELAY_MS); });
                    continue;
                }
                throw err;
            }
        }
    }

    function handleErrorResponse(json) {
        if (json.errorCode === 'RATE_LIMITED') {
            showAlert(json.message || 'Too many requests. Please wait a moment before sending again.', true, true);
            return;
        }
        if (json.errorCode === 'AI_PROVIDER_ERROR' || json.errorCode === 'TOOL_EXECUTION_ERROR') {
            appendMessage('ASSISTANT',
                'Sorry, I encountered an issue processing your request. Please try again in a moment.',
                json.message || 'AI service temporarily unavailable');
            return;
        }
        if (json.errorCode === 'INVALID_AI_SCHEMA') {
            appendMessage('ASSISTANT',
                'AI response chưa đúng định dạng, vui lòng thử lại.',
                json.message || 'Response format error');
            return;
        }
        showAlert(json.message || 'An error occurred.', true, false);
    }

    async function startSession() {
        if (isSending) return;
        hideAlert();
        var purpose = purposeSelect.value;

        setLoading(true);
        try {
            var response = await fetchWithRetry('?handler=StartSession', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify({ purpose: purpose })
            }, MAX_NETWORK_RETRIES);

            var json = await response.json();
            if (!response.ok || !json.isSuccess) {
                handleErrorResponse(json);
                return;
            }

            currentSessionId = json.data.chatSessionId;
            messageList.innerHTML = '';
            showChat();
            updateSessionInfo(json.data);
            showAlert('Session started! Ask your first question.', false, false);
            messageInput.focus();
        } catch (err) {
            showAlert('Network error. Please check your connection and try again.', true, false);
        } finally {
            setLoading(false);
        }
    }

    async function sendMessage() {
        if (isSending || !currentSessionId) return;
        var text = messageInput.value.trim();
        if (!text) return;

        hideAlert();
        appendMessage('USER', text);
        messageInput.value = '';
        autoResizeInput();
        sendBtn.disabled = true;

        setLoading(true);
        try {
            var response = await fetchWithRetry('?handler=SendMessage', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify({
                    chatSessionId: currentSessionId,
                    message: text,
                    useTools: true
                })
            }, MAX_NETWORK_RETRIES);

            var json = await response.json();
            if (!response.ok || !json.isSuccess) {
                handleErrorResponse(json);
                return;
            }

            var turn = json.data;
            var assistantContent = turn.assistantMessage ? turn.assistantMessage.content : '';
            var warning = turn.warning || null;
            var structured = turn.structuredData || null;
            appendMessage('ASSISTANT', assistantContent, warning, structured);

            if (turn.session) {
                updateSessionInfo(turn.session);
            }
        } catch (err) {
            appendMessage('ASSISTANT',
                'Sorry, a network error occurred. Please check your connection and try again.',
                'Network error');
        } finally {
            setLoading(false);
        }
    }

    function autoResizeInput() {
        messageInput.style.height = 'auto';
        messageInput.style.height = Math.min(messageInput.scrollHeight, 120) + 'px';
    }

    // Event listeners
    newSessionBtn.addEventListener('click', startSession);
    sendBtn.addEventListener('click', sendMessage);

    messageInput.addEventListener('input', function () {
        sendBtn.disabled = isSending || !messageInput.value.trim();
        autoResizeInput();
    });

    messageInput.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            if (!isSending && messageInput.value.trim() && currentSessionId) {
                sendMessage();
            }
        }
    });
})();
