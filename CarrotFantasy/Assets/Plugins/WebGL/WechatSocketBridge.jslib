mergeInto(LibraryManager.library, (function () {
    var state = {
        gameObjectName: "",
        socket: null
    };

    function sendToUnity(method, payload) {
        if (!state.gameObjectName) {
            return;
        }

        if (typeof SendMessage === "function") {
            SendMessage(state.gameObjectName, method, payload || "");
        }
    }

    function base64ToArrayBuffer(base64) {
        var binary = atob(base64);
        var length = binary.length;
        var bytes = new Uint8Array(length);
        for (var i = 0; i < length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        return bytes.buffer;
    }

    function arrayBufferToBase64(buffer) {
        var bytes = new Uint8Array(buffer);
        var binary = "";
        for (var i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return btoa(binary);
    }

    function attachSocketEvents(socket) {
        if (!socket) {
            return;
        }

        socket.onOpen(function () {
            sendToUnity("OnWechatSocketOpen", "");
        });

        socket.onClose(function (res) {
            var code = (res && typeof res.code !== "undefined") ? res.code : 0;
            var reason = (res && res.reason) ? res.reason : "";
            sendToUnity("OnWechatSocketClose", String(code) + "|" + reason);
        });

        socket.onError(function (res) {
            var msg = "";
            if (res && res.errMsg) {
                msg = res.errMsg;
            }
            sendToUnity("OnWechatSocketError", msg);
        });

        socket.onMessage(function (res) {
            if (!res || typeof res.data === "undefined") {
                sendToUnity("OnWechatSocketError", "socket message data is undefined");
                return;
            }

            try {
                var data = res.data;
                if (typeof data === "string") {
                    sendToUnity("OnWechatSocketMessage", data);
                    return;
                }

                if (data instanceof ArrayBuffer) {
                    sendToUnity("OnWechatSocketMessage", arrayBufferToBase64(data));
                    return;
                }

                if (typeof Uint8Array !== "undefined" && data instanceof Uint8Array) {
                    sendToUnity("OnWechatSocketMessage", arrayBufferToBase64(data.buffer));
                    return;
                }

                sendToUnity("OnWechatSocketError", "unsupported message data type");
            } catch (e) {
                sendToUnity("OnWechatSocketError", "message parse failed: " + e.message);
            }
        });
    }

    return {
        WechatSocket_SetGameObjectName: function (namePtr) {
            state.gameObjectName = UTF8ToString(namePtr);
        },

        WechatSocket_Connect: function (urlPtr) {
            var url = UTF8ToString(urlPtr);
            if (state.socket && state.socket.close) {
                try {
                    state.socket.close({});
                } catch (e) {
                    // ignored
                }
            }

            if (typeof wx === "undefined" || !wx.connectSocket) {
                sendToUnity("OnWechatSocketError", "wx.connectSocket unavailable");
                return;
            }

            try {
                state.socket = wx.connectSocket({
                    url: url,
                    protocols: [],
                    tcpNoDelay: true
                });
                attachSocketEvents(state.socket);
            } catch (e) {
                sendToUnity("OnWechatSocketError", "connect failed: " + e.message);
            }
        },

        WechatSocket_Send: function (base64Ptr) {
            if (!state.socket || !state.socket.send) {
                sendToUnity("OnWechatSocketError", "socket is not connected");
                return;
            }

            try {
                var base64 = UTF8ToString(base64Ptr);
                var buffer = base64ToArrayBuffer(base64);
                state.socket.send({
                    data: buffer
                });
            } catch (e) {
                sendToUnity("OnWechatSocketError", "send failed: " + e.message);
            }
        },

        WechatSocket_Close: function () {
            if (!state.socket || !state.socket.close) {
                return;
            }

            try {
                state.socket.close({});
            } catch (e) {
                sendToUnity("OnWechatSocketError", "close failed: " + e.message);
            }
        }
    };
})());
