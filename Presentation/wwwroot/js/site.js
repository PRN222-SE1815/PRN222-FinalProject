(function () {
    var panel = null;
    var countEl = null;
    var listEl = null;
    var initialized = false;

    function getBaseUrl() {
        return '/Notifications';
    }

    function getAntiForgeryToken() {
        var tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenEl ? tokenEl.value : '';
    }

    function updateBadge(count) {
        if (!countEl) {
            return;
        }

        var safeCount = Number.isFinite(count) ? count : 0;
        countEl.textContent = safeCount.toString();
        countEl.style.display = safeCount > 0 ? 'flex' : 'none';
    }

    function escapeHtml(value) {
        if (!value) {
            return '';
        }

        return value
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function formatTime(value) {
        if (!value) {
            return '';
        }

        var when = new Date(value);
        if (Number.isNaN(when.getTime())) {
            return value;
        }

        var diffMs = Date.now() - when.getTime();
        if (diffMs < 0) {
            return when.toLocaleString('vi-VN');
        }

        var minuteMs = 60000;
        var hourMs = 60 * minuteMs;
        var dayMs = 24 * hourMs;

        if (diffMs < minuteMs) {
            return 'just now';
        }

        if (diffMs < hourMs) {
            return Math.floor(diffMs / minuteMs) + 'm ago';
        }

        if (diffMs < dayMs) {
            return Math.floor(diffMs / hourMs) + 'h ago';
        }

        return when.getFullYear() + '-'
            + String(when.getMonth() + 1).padStart(2, '0') + '-'
            + String(when.getDate()).padStart(2, '0') + ' '
            + String(when.getHours()).padStart(2, '0') + ':'
            + String(when.getMinutes()).padStart(2, '0');
    }

    function buildItem(notification) {
        var item = document.createElement('div');
        item.className = 'notification-item' + (notification.isRead ? '' : ' unread');
        item.dataset.notificationId = notification.notificationId;
        item.dataset.deepLink = notification.deepLink || '';

        var title = escapeHtml(notification.title || 'Notification');
        var message = escapeHtml(notification.message || '');
        var timeText = formatTime(notification.createdAtUtc);

        item.innerHTML = ''
            + '<div class="notification-content">'
            + '  <div class="notification-title">' + title + '</div>'
            + '  <div class="notification-meta">' + message + '</div>'
            + '  <div class="notification-time">' + escapeHtml(timeText) + '</div>'
            + '</div>';

        item.addEventListener('click', function () {
            handleItemClick(item);
        });

        return item;
    }

    function renderNotifications(items) {
        if (!listEl) {
            return;
        }

        listEl.innerHTML = '';
        if (!items || items.length === 0) {
            listEl.innerHTML = '<div class="empty-notifications"><i class="fas fa-bell-slash"></i><p>No new notifications</p></div>';
            return;
        }

        items.forEach(function (item) {
            listEl.appendChild(buildItem(item));
        });
    }

    async function loadNotifications() {
        var response = await fetch(getBaseUrl() + '?handler=Notifications&page=1&pageSize=20');
        if (!response.ok) {
            return;
        }

        var payload = await response.json();
        renderNotifications(payload.items || []);
    }

    async function loadUnreadCount() {
        var response = await fetch(getBaseUrl() + '?handler=UnreadCount');
        if (!response.ok) {
            return;
        }

        var payload = await response.json();
        var count = payload.count ?? payload.Count ?? 0;
        updateBadge(Number(count));
    }

    async function markAsRead(notificationId) {
        var token = getAntiForgeryToken();
        var body = new URLSearchParams();
        body.append('notificationId', String(notificationId));

        await fetch(getBaseUrl() + '?handler=MarkRead', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                'RequestVerificationToken': token
            },
            body: body.toString()
        });
    }

    async function markAllAsRead() {
        var token = getAntiForgeryToken();

        await fetch(getBaseUrl() + '?handler=MarkAllRead', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                'RequestVerificationToken': token
            }
        });
    }

    async function handleItemClick(item) {
        var notificationId = item.dataset.notificationId;
        var deepLink = item.dataset.deepLink;
        var isUnread = item.classList.contains('unread');

        if (isUnread && notificationId) {
            await markAsRead(notificationId);
            item.classList.remove('unread');
            var currentCount = parseInt(countEl && countEl.textContent ? countEl.textContent : '0', 10);
            updateBadge(Math.max(0, currentCount - 1));
        }

        if (deepLink) {
            window.location.href = deepLink;
        }
    }

    function prependNotification(notification) {
        if (!listEl) {
            return;
        }

        var emptyState = listEl.querySelector('.empty-notifications');
        if (emptyState) {
            emptyState.remove();
        }

        listEl.prepend(buildItem(notification));
        var currentCount = parseInt(countEl && countEl.textContent ? countEl.textContent : '0', 10);
        updateBadge((Number.isNaN(currentCount) ? 0 : currentCount) + 1);
    }

    async function connectNotificationHub() {
        if (!window.signalR) {
            return;
        }

        var connection = new signalR.HubConnectionBuilder()
            .withUrl('/notificationHub')
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .build();

        connection.on('ReceiveNotification', function (notification) {
            prependNotification(notification);
        });

        connection.on('CourseCreated', function (payload) {
            var code = (payload && payload.courseCode) || '';
            showRealtimeToast('info', 'Course Created', 'New course added: ' + code);
            reloadIfOnCoursePage();
            reloadIfOnMyCoursesPage();
        });

        connection.on('CourseUpdated', function (payload) {
            var code = (payload && payload.courseCode) || '';
            showRealtimeToast('info', 'Course Updated', 'Course updated: ' + code);
            reloadIfOnCoursePage();
            reloadIfOnMyCoursesPage();
        });

        connection.on('CourseDeactivated', function (payload) {
            var code = (payload && payload.courseCode) || '';
            var reason = (payload && payload.reason) || '';
            var dropped = (payload && payload.droppedEnrollmentCount) || 0;
            var msg = 'Course ' + code + ' has been deactivated.';
            if (reason) { msg += ' Reason: ' + reason + '.'; }
            if (dropped > 0) { msg += ' (' + dropped + ' enrollments dropped)'; }
            showRealtimeToast('warning', 'Course Deactivated', msg);
            reloadIfOnCoursePage();
            reloadIfOnMyCoursesPage();
        });

        connection.on('CourseEnrollmentsDropped', function (payload) {
            var code = (payload && payload.courseCode) || '';
            var dropped = (payload && payload.droppedEnrollmentCount) || 0;
            showRealtimeToast('warning', 'Enrollments Dropped', dropped + ' enrollments dropped for ' + code);
            reloadIfOnMyCoursesPage();
        });

        connection.onreconnected(function () {
            loadNotifications();
            loadUnreadCount();
        });

        try {
            await connection.start();
        } catch (e) {
            console.error('Notification hub connection failed:', e);
        }
    }

    function reloadIfOnCoursePage() {
        var path = window.location.pathname.toLowerCase();
        if (path.indexOf('/admin/coursemanagement') !== -1) {
            setTimeout(function () { window.location.reload(); }, 1500);
        }
    }

    function reloadIfOnMyCoursesPage() {
        var path = window.location.pathname.toLowerCase();
        if (path.indexOf('/student/mycourses') !== -1
            || path.indexOf('/student/courseregistration') !== -1
            || path.indexOf('/teacher/myclasses') !== -1) {
            setTimeout(function () { window.location.reload(); }, 1500);
        }
    }

    function ensureToastHost() {
        var host = document.getElementById('globalToastHost');
        if (!host) {
            host = document.createElement('div');
            host.id = 'globalToastHost';
            document.body.appendChild(host);
        }
        return host;
    }

    function showRealtimeToast(type, title, message) {
        var host = ensureToastHost();
        var toast = document.createElement('div');
        toast.className = 'toast toast-' + (type || 'info');
        toast.innerHTML = '<strong>' + escapeHtml(title || '') + '</strong>&nbsp; ' + escapeHtml(message || '');
        host.appendChild(toast);
        setTimeout(function () {
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(30px)';
            toast.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
            setTimeout(function () { toast.remove(); }, 350);
        }, 5000);
    }

    async function initializeNotifications() {
        panel = document.getElementById('notificationPanel');
        countEl = document.getElementById('notificationCount');
        listEl = document.getElementById('notificationList');

        if (!panel || !countEl || !listEl || initialized) {
            return;
        }

        initialized = true;
        await loadNotifications();
        await loadUnreadCount();
        await connectNotificationHub();

        document.addEventListener('click', function (event) {
            var bell = document.getElementById('notificationBell');
            if (!bell || bell.contains(event.target)) {
                return;
            }

            panel.classList.remove('open');
        });
    }

    window.toggleNotificationPanel = function () {
        if (!panel) {
            panel = document.getElementById('notificationPanel');
        }

        if (!panel) {
            return;
        }

        panel.classList.toggle('open');
    };

    window.clearAllNotifications = async function () {
        await markAllAsRead();
        updateBadge(0);
        renderNotifications([]);
    };

    document.addEventListener('DOMContentLoaded', function () {
        initializeNotifications();
    });
})();
