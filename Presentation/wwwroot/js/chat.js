// ============================================================
// chat.js — SignalR client for Chat feature (Razor Pages)
// Matches ChatHub method names and DTO shapes exactly.
// ============================================================

"use strict";

// ==================== State ====================
const currentUserId = parseInt(document.getElementById("currentUserId").value, 10);
let selectedRoomId = parseInt(document.getElementById("selectedRoomId").value, 10) || 0;
let editingMessageId = null;
let oldestLoadedMessageId = null;
let searchDebounceTimer = null;
let groupSearchDebounceTimer = null;
const selectedGroupMembers = new Map();
// Pending file attachments: array of { fileUrl, fileType, fileSizeBytes, originalName }
let pendingAttachments = [];

// ==================== SignalR Connection ====================
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

// ==================== Hub Event Handlers ====================

connection.on("ReceiveMessage", function (msg) {
    if (msg.roomId === selectedRoomId) {
        appendMessage(msg);
        // Mark as read
        connection.invoke("MarkRead", selectedRoomId, msg.messageId).catch(logError);
    }
});

connection.on("MessageEdited", function (data) {
    if (data.roomId === selectedRoomId) {
        const row = document.querySelector(`.message-row[data-message-id="${data.messageId}"]`);
        if (row) {
            const contentEl = row.querySelector(".message-content");
            if (contentEl) contentEl.textContent = data.content;
            // Show (edited) indicator
            let editedSpan = row.querySelector(".message-edited");
            if (!editedSpan) {
                editedSpan = document.createElement("span");
                editedSpan.className = "message-edited";
                editedSpan.textContent = "(edited)";
                const bubble = row.querySelector(".message-bubble");
                const timeEl = bubble.querySelector(".message-time");
                bubble.insertBefore(editedSpan, timeEl);
            }
        }
    }
});

connection.on("MessageDeleted", function (data) {
    if (data.roomId === selectedRoomId) {
        const row = document.querySelector(`.message-row[data-message-id="${data.messageId}"]`);
        if (row) {
            row.classList.add("deleted");
            const bubble = row.querySelector(".message-bubble");
            bubble.innerHTML =
                `<span class="message-deleted-text"><i class="fas fa-ban"></i> This message was deleted</span>` +
                `<span class="message-time">${bubble.querySelector(".message-time")?.outerHTML || ""}</span>`;
            // Remove action buttons
            const actions = row.querySelector(".message-actions");
            if (actions) actions.remove();
        }
    }
});

connection.on("RoomCreated", function (room) {
    // Add to sidebar and select it
    addRoomToSidebar(room);
    closeNewChatModal();
    selectRoom(room.roomId);
});

connection.on("Error", function (message) {
    showToast(message, "error");
});

// ==================== Connection Management ====================

connection.onreconnecting(function () {
    showToast("Reconnecting to chat...", "warning");
});

connection.onreconnected(function () {
    showToast("Reconnected!", "success");
    // Re-join current room
    if (selectedRoomId > 0) {
        connection.invoke("JoinRoom", selectedRoomId).catch(logError);
    }
});

connection.onclose(function () {
    showToast("Disconnected from chat. Please refresh.", "error");
});

async function startConnection() {
    try {
        await connection.start();
        console.log("ChatHub connected.");
    } catch (err) {
        console.error("ChatHub connection error:", err);
        setTimeout(startConnection, 5000);
    }
}

startConnection();

// ==================== Room Selection ====================

function selectRoom(roomId) {
    // Navigate with full page reload so server loads messages + sets SelectedRoom
    window.location.href = `/Chat/${roomId}`;
}

function showSidebar() {
    const sidebar = document.getElementById("chatSidebar");
    const main = document.getElementById("chatMain");
    if (sidebar) sidebar.classList.add("show-mobile");
    if (main) main.classList.remove("show-mobile");
}

// ==================== Sending Messages ====================

async function handleSendMessage(e) {
    e.preventDefault();
    const input = document.getElementById("messageInput");
    const content = input.value.trim();

    if (!content && pendingAttachments.length === 0) return false;
    if (selectedRoomId === 0) return false;

    if (editingMessageId) {
        // Edit mode — no attachments in edit
        connection.invoke("EditMessage", selectedRoomId, editingMessageId, content).catch(logError);
        cancelEdit();
    } else if (pendingAttachments.length > 0) {
        // Send with attachments
        connection.invoke("SendMessageWithAttachments", selectedRoomId, content || null, pendingAttachments).catch(logError);
        clearPendingAttachments();
    } else {
        // Plain text
        connection.invoke("SendMessage", selectedRoomId, content).catch(logError);
    }

    return false;
}

// ==================== File Upload ====================

async function handleFileSelected(event) {
    const files = Array.from(event.target.files);
    if (!files.length) return;

    // Reset input so same file can be re-selected
    event.target.value = "";

    for (const file of files) {
        await uploadFile(file);
    }
}

async function uploadFile(file) {
    const maxSize = 20 * 1024 * 1024;
    if (file.size > maxSize) {
        showToast(`"${file.name}" exceeds the 20 MB limit.`, "error");
        return;
    }

    const previewBar = document.getElementById("attachmentPreviewBar");
    previewBar.style.display = "flex";

    // Add a loading chip
    const chipId = `chip-${Date.now()}-${Math.random().toString(36).slice(2)}`;
    const chip = document.createElement("div");
    chip.className = "attach-chip loading";
    chip.id = chipId;
    chip.innerHTML = `<i class="fas fa-spinner fa-spin"></i><span>${escapeHtml(file.name)}</span>`;
    previewBar.appendChild(chip);

    try {
        const formData = new FormData();
        formData.append("file", file);

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const headers = {};
        if (token) headers["RequestVerificationToken"] = token;

        const resp = await fetch("/Chat?handler=UploadFile", {
            method: "POST",
            headers,
            body: formData
        });

        if (!resp.ok) {
            const err = await resp.json().catch(() => ({ error: "Upload failed." }));
            showToast(err.error || "Upload failed.", "error");
            chip.remove();
            if (previewBar.children.length === 0) previewBar.style.display = "none";
            return;
        }

        const data = await resp.json();
        pendingAttachments.push({
            fileUrl: data.fileUrl,
            fileType: data.fileType,
            fileSizeBytes: data.fileSizeBytes,
            originalName: data.originalName
        });

        // Update chip to success
        const isImage = ["jpg", "jpeg", "png", "gif", "webp"].includes(data.fileType.toLowerCase());
        chip.className = "attach-chip";
        chip.innerHTML = isImage
            ? `<img src="${escapeHtml(data.fileUrl)}" class="chip-thumb" alt="${escapeHtml(data.originalName)}" />
               <span>${escapeHtml(data.originalName)}</span>
               <button type="button" onclick="removePendingAttachment('${escapeHtml(data.fileUrl)}','${chipId}')"><i class="fas fa-times"></i></button>`
            : `<i class="fas fa-file"></i>
               <span>${escapeHtml(data.originalName)}</span>
               <button type="button" onclick="removePendingAttachment('${escapeHtml(data.fileUrl)}','${chipId}')"><i class="fas fa-times"></i></button>`;

    } catch (err) {
        showToast("Upload error: " + err.message, "error");
        chip.remove();
        if (previewBar.children.length === 0) previewBar.style.display = "none";
    }
}

function removePendingAttachment(fileUrl, chipId) {
    pendingAttachments = pendingAttachments.filter(a => a.fileUrl !== fileUrl);
    const chip = document.getElementById(chipId);
    if (chip) chip.remove();
    const previewBar = document.getElementById("attachmentPreviewBar");
    if (previewBar && previewBar.children.length === 0) previewBar.style.display = "none";
}

function clearPendingAttachments() {
    pendingAttachments = [];
    const previewBar = document.getElementById("attachmentPreviewBar");
    if (previewBar) {
        previewBar.innerHTML = "";
        previewBar.style.display = "none";
    }
}

// ==================== Edit / Delete ====================

function startEditMessage(messageId) {
    const row = document.querySelector(`.message-row[data-message-id="${messageId}"]`);
    if (!row) return;
    const contentEl = row.querySelector(".message-content");
    if (!contentEl) return;

    editingMessageId = messageId;
    const input = document.getElementById("messageInput");
    input.value = contentEl.textContent;
    input.focus();

    document.getElementById("editIndicator").style.display = "flex";
}

function cancelEdit() {
    editingMessageId = null;
    document.getElementById("messageInput").value = "";
    document.getElementById("editIndicator").style.display = "none";
    clearPendingAttachments();
}

function deleteMessage(messageId) {
    if (!confirm("Delete this message?")) return;
    connection.invoke("DeleteMessage", selectedRoomId, messageId).catch(logError);
}

// ==================== Load Older Messages (cursor paging) ====================

function loadOlderMessages() {
    if (!oldestLoadedMessageId || selectedRoomId === 0) return;

    fetch(`/Chat?handler=Messages&roomId=${selectedRoomId}&beforeMessageId=${oldestLoadedMessageId}`)
        .then(r => r.json())
        .then(data => {
            if (data.items && data.items.length > 0) {
                const container = document.getElementById("chatMessages");
                const loadMoreBtn = document.getElementById("loadMoreContainer");
                data.items.forEach(function (msg) {
                    const el = buildMessageElement(msg);
                    container.insertBefore(el, loadMoreBtn.nextSibling);
                });
                oldestLoadedMessageId = data.items[data.items.length - 1].messageId;
                if (data.items.length < data.pageSize) {
                    loadMoreBtn.style.display = "none";
                }
            }
        })
        .catch(logError);
}

// ==================== Initialize oldest message id for paging ====================

(function initPaging() {
    const msgs = document.querySelectorAll(".message-row[data-message-id]");
    if (msgs.length > 0) {
        oldestLoadedMessageId = parseInt(msgs[0].getAttribute("data-message-id"), 10);
        if (msgs.length >= 20) {
            const btn = document.getElementById("loadMoreContainer");
            if (btn) btn.style.display = "block";
        }
    }
    scrollToBottom();
})();

// ==================== DOM Helpers ====================

function appendMessage(msg) {
    // Remove "no messages" placeholder if present
    const placeholder = document.getElementById("noMessagesPlaceholder");
    if (placeholder) placeholder.remove();

    const container = document.getElementById("chatMessages");
    const el = buildMessageElement(msg);
    container.appendChild(el);

    // Scroll the new message into view after paint
    requestAnimationFrame(function () {
        el.scrollIntoView({ behavior: "smooth", block: "end" });
    });
}

function scrollToBottom() {
    requestAnimationFrame(function () {
        const container = document.getElementById("chatMessages");
        if (container) {
            container.scrollTop = container.scrollHeight;
        }
    });
}

function buildMessageElement(msg) {
    const isMine = msg.senderId === currentUserId;
    const isDeleted = !!msg.deletedAt;

    const row = document.createElement("div");
    row.className = `message-row ${isMine ? "mine" : "theirs"} ${isDeleted ? "deleted" : ""}`;
    row.setAttribute("data-message-id", msg.messageId);
    row.setAttribute("data-sender-id", msg.senderId);

    let bubbleHtml = "";

    if (isDeleted) {
        bubbleHtml = `<span class="message-deleted-text"><i class="fas fa-ban"></i> This message was deleted</span>`;
    } else {
        if (!isMine) {
            bubbleHtml += `<span class="message-sender">${escapeHtml(msg.senderName || "Unknown")}</span>`;
        }
        bubbleHtml += `<span class="message-content">${escapeHtml(msg.content || "")}</span>`;
        if (msg.attachments && msg.attachments.length > 0) {
            bubbleHtml += `<div class="message-attachments">`;
            msg.attachments.forEach(function (att) {
                const imgExts = ["jpg", "jpeg", "png", "gif", "webp"];
                const isImage = imgExts.includes((att.fileType || "").toLowerCase());
                const displayName = att.originalName || att.fileType || "file";
                if (isImage) {
                    bubbleHtml += `<a href="${escapeHtml(att.fileUrl)}" target="_blank" class="attachment-image-link">
                        <img src="${escapeHtml(att.fileUrl)}" class="attachment-thumbnail" alt="${escapeHtml(displayName)}" />
                    </a>`;
                } else {
                    bubbleHtml += `<a href="${escapeHtml(att.fileUrl)}" target="_blank" class="attachment-file-link">
                        <i class="fas fa-file"></i> <span>${escapeHtml(displayName)}</span>
                    </a>`;
                }
            });
            bubbleHtml += `</div>`;
        }
        if (msg.editedAt) {
            bubbleHtml += `<span class="message-edited">(edited)</span>`;
        }
    }

    const time = new Date(msg.createdAt);
    const timeStr = time.toLocaleDateString("en-US", { month: "short", day: "2-digit" }) +
        ", " + time.toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit", hour12: false });
    bubbleHtml += `<span class="message-time">${timeStr}</span>`;

    let actionsHtml = "";
    
    const canEdit = !isDeleted && isMine;
    const canDelete = !isDeleted && isMine;

    if (canEdit || canDelete) {
        actionsHtml = `<div class="message-actions">`;
        if (canEdit) {
            actionsHtml += `<button type="button" class="btn-msg-action" title="Edit" onclick="startEditMessage(${msg.messageId})"><i class="fas fa-pen"></i></button>`;
        }
        if (canDelete) {
            actionsHtml += `<button type="button" class="btn-msg-action btn-danger" title="Delete" onclick="deleteMessage(${msg.messageId})"><i class="fas fa-trash"></i></button>`;
        }
        actionsHtml += `</div>`;
    }

    row.innerHTML = `<div class="message-bubble">${bubbleHtml}</div>${actionsHtml}`;
    return row;
}

function scrollToBottom() {
    const container = document.getElementById("chatMessages");
    if (container) {
        container.scrollTop = container.scrollHeight;
    }
}

function escapeHtml(text) {
    const div = document.createElement("div");
    div.appendChild(document.createTextNode(text));
    return div.innerHTML;
}

// ==================== Room Sidebar Helpers ====================

function addRoomToSidebar(room) {
    const list = document.getElementById("roomList");
    // Remove "no conversations" placeholder
    const emptyLi = list.querySelector(".room-empty");
    if (emptyLi) emptyLi.remove();

    // Don't add duplicate
    if (list.querySelector(`[data-room-id="${room.roomId}"]`)) return;

    const iconClass = room.roomType === "DM" ? "fa-user"
        : room.roomType === "GROUP" ? "fa-users"
            : room.roomType === "CLASS" ? "fa-chalkboard"
                : "fa-book";

    const li = document.createElement("li");
    li.className = "room-item";
    li.setAttribute("data-room-id", room.roomId);
    li.setAttribute("data-room-type", room.roomType);
    li.setAttribute("onclick", `selectRoom(${room.roomId})`);
    li.innerHTML = `
        <div class="room-icon"><i class="fas ${iconClass}"></i></div>
        <div class="room-info">
            <span class="room-name">${escapeHtml(room.roomName)}</span>
            <span class="room-type-badge">${escapeHtml(room.roomType)}</span>
        </div>`;
    list.prepend(li);
}

// ==================== Room Search (client-side filter) ====================

(function initRoomSearch() {
    const input = document.getElementById("roomSearchInput");
    if (!input) return;
    input.addEventListener("input", function () {
        const q = this.value.toLowerCase();
        document.querySelectorAll(".room-item").forEach(function (li) {
            const name = li.querySelector(".room-name")?.textContent?.toLowerCase() || "";
            li.style.display = name.includes(q) ? "" : "none";
        });
    });
})();

// ==================== New Chat Modal ====================

document.getElementById("btnNewChat")?.addEventListener("click", function () {
    document.getElementById("newChatModal").style.display = "flex";
});

function closeNewChatModal() {
    document.getElementById("newChatModal").style.display = "none";
    // Reset
    document.getElementById("userSearchInput").value = "";
    document.getElementById("userSearchResults").innerHTML = '<li class="search-placeholder">Type to search for users...</li>';
    document.getElementById("groupNameInput").value = "";
    document.getElementById("groupUserSearchInput").value = "";
    document.getElementById("groupUserSearchResults").innerHTML = '<li class="search-placeholder">Type to search for users...</li>';
    selectedGroupMembers.clear();
    renderSelectedMembers();
}

// ==================== Modal Tabs ====================

function switchTab(tab) {
    document.querySelectorAll(".tab-btn").forEach(function (btn) {
        btn.classList.toggle("active", btn.getAttribute("data-tab") === tab);
    });
    document.getElementById("tabDm").classList.toggle("active", tab === "dm");
    document.getElementById("tabGroup").classList.toggle("active", tab === "group");
}

// ==================== User Search (DM) ====================

function searchUsers() {
    clearTimeout(searchDebounceTimer);
    searchDebounceTimer = setTimeout(function () {
        const q = document.getElementById("userSearchInput").value.trim();
        if (q.length < 1) {
            document.getElementById("userSearchResults").innerHTML = '<li class="search-placeholder">Type to search for users...</li>';
            return;
        }
        fetch(`/Chat?handler=SearchUsers&search=${encodeURIComponent(q)}`)
            .then(r => r.json())
            .then(users => {
                const list = document.getElementById("userSearchResults");
                if (!users || users.length === 0) {
                    list.innerHTML = '<li class="search-placeholder">No users found.</li>';
                    return;
                }
                list.innerHTML = users.map(function (u) {
                    return `<li class="user-item" onclick="startDm(${u.userId})">
                        <span class="user-name-result">${escapeHtml(u.fullName)}</span>
                        <span class="user-role-badge">${escapeHtml(u.role)}</span>
                    </li>`;
                }).join("");
            })
            .catch(logError);
    }, 300);
}

function startDm(otherUserId) {
    connection.invoke("CreateOrGetDmRoom", otherUserId).catch(logError);
}

// ==================== User Search (Group) ====================

function searchGroupUsers() {
    clearTimeout(groupSearchDebounceTimer);
    groupSearchDebounceTimer = setTimeout(function () {
        const q = document.getElementById("groupUserSearchInput").value.trim();
        if (q.length < 1) {
            document.getElementById("groupUserSearchResults").innerHTML = '<li class="search-placeholder">Type to search for users...</li>';
            return;
        }
        fetch(`/Chat?handler=SearchUsers&search=${encodeURIComponent(q)}`)
            .then(r => r.json())
            .then(users => {
                const list = document.getElementById("groupUserSearchResults");
                if (!users || users.length === 0) {
                    list.innerHTML = '<li class="search-placeholder">No users found.</li>';
                    return;
                }
                list.innerHTML = users.map(function (u) {
                    const selected = selectedGroupMembers.has(u.userId);
                    return `<li class="user-item ${selected ? "selected" : ""}" onclick="toggleGroupMember(${u.userId}, '${escapeHtml(u.fullName)}', '${escapeHtml(u.role)}')">
                        <span class="user-name-result">${escapeHtml(u.fullName)}</span>
                        <span class="user-role-badge">${escapeHtml(u.role)}</span>
                        ${selected ? '<i class="fas fa-check"></i>' : ""}
                    </li>`;
                }).join("");
            })
            .catch(logError);
    }, 300);
}

function toggleGroupMember(userId, fullName, role) {
    if (selectedGroupMembers.has(userId)) {
        selectedGroupMembers.delete(userId);
    } else {
        selectedGroupMembers.set(userId, { userId, fullName, role });
    }
    renderSelectedMembers();
    // Re-render search results to reflect selection state
    searchGroupUsers();
}

function renderSelectedMembers() {
    const container = document.getElementById("selectedMembers");
    if (selectedGroupMembers.size === 0) {
        container.innerHTML = "";
        return;
    }
    let html = "";
    selectedGroupMembers.forEach(function (m) {
        html += `<span class="member-chip">
            ${escapeHtml(m.fullName)}
            <button type="button" onclick="toggleGroupMember(${m.userId}, '${escapeHtml(m.fullName)}', '${escapeHtml(m.role)}')"><i class="fas fa-times"></i></button>
        </span>`;
    });
    container.innerHTML = html;
}

function createGroupRoom() {
    const name = document.getElementById("groupNameInput").value.trim();
    if (!name) {
        showToast("Group name is required.", "error");
        return;
    }
    const memberIds = Array.from(selectedGroupMembers.keys());
    connection.invoke("CreateGroupRoom", name, memberIds).catch(logError);
}

// ==================== Toast Notification ====================

function showToast(message, type) {
    // Remove existing toast
    const existing = document.querySelector(".chat-toast");
    if (existing) existing.remove();

    const toast = document.createElement("div");
    toast.className = `chat-toast toast-${type || "info"}`;
    toast.innerHTML = `<span>${escapeHtml(message)}</span>`;
    document.body.appendChild(toast);

    setTimeout(function () {
        toast.classList.add("fade-out");
        setTimeout(function () { toast.remove(); }, 400);
    }, 3500);
}

// ==================== Utilities ====================

function logError(err) {
    console.error("ChatHub error:", err);
}

// Mark latest message as read on initial load
(function markInitialRead() {
    if (selectedRoomId > 0) {
        const msgs = document.querySelectorAll(".message-row[data-message-id]");
        if (msgs.length > 0) {
            const lastId = parseInt(msgs[msgs.length - 1].getAttribute("data-message-id"), 10);
            if (lastId) {
                // Wait for connection to be ready
                connection.start ? void 0 : null; // no-op guard
                const tryMark = function () {
                    if (connection.state === "Connected") {
                        connection.invoke("MarkRead", selectedRoomId, lastId).catch(logError);
                    } else {
                        setTimeout(tryMark, 500);
                    }
                };
                setTimeout(tryMark, 1000);
            }
        }
    }
})();
