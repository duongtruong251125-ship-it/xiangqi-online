const { createApp } = Vue;
let accessToken = null;
let accessTokenExpiresAt = 0;

const initialPieces = [
  ...['車', '馬', '象', '士', '將', '士', '象', '馬', '車'].map((name, file) => ({ name, file, rank: 0, side: 'black' })),
  { name: '砲', file: 1, rank: 2, side: 'black' }, { name: '砲', file: 7, rank: 2, side: 'black' },
  ...[0, 2, 4, 6, 8].map(file => ({ name: '卒', file, rank: 3, side: 'black' })),
  ...['俥', '傌', '相', '仕', '帥', '仕', '相', '傌', '俥'].map((name, file) => ({ name, file, rank: 9, side: 'red' })),
  { name: '炮', file: 1, rank: 7, side: 'red' }, { name: '炮', file: 7, rank: 7, side: 'red' },
  ...[0, 2, 4, 6, 8].map(file => ({ name: '兵', file, rank: 6, side: 'red' })),
];

async function api(path, method = 'GET', body = null) {
  const headers = { Accept: 'application/json' };
  if (body !== null) headers['Content-Type'] = 'application/json';
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
  const response = await fetch(`/api/v1${path}`, {
    method, headers, credentials: 'include', body: body === null ? undefined : JSON.stringify(body),
  });
  const result = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(result.error?.message || 'Không thể kết nối máy chủ.');
  return result.data;
}

async function ensureSignalRToken() {
  if (!accessToken || Date.now() >= accessTokenExpiresAt - 60000) {
    const session = await api('/auth/refresh', 'POST', {});
    accessToken = session.accessToken;
    accessTokenExpiresAt = Date.now() + session.expiresIn * 1000;
  }
  return accessToken;
}

const pageTemplates = {
  home: `<section class="hero"><div><p class="eyebrow">KỲ ĐÀI HOÀNG GIA · CỜ TƯỚNG TRỰC TUYẾN</p><h1>Giữ vững<br><em>thế cờ.</em></h1><p>Không gian đối dịch sang trọng với bàn cờ chuẩn luật máy chủ, phòng đấu trực tuyến thời gian thực, đàm thoại Video 1–1 và trí tuệ nhân tạo Alpha-Beta.</p><div class="hero-cta-row"><a class="button" href="/Lobby">Khám phá sảnh chơi</a><a class="button secondary" href="/AiGame/Index">Luyện cờ với Máy</a><a class="link-button" href="/Account/Register">Tạo tài khoản</a></div><div class="hero-pillars"><div class="hero-pillar"><strong>Chuẩn Luật Máy Chủ</strong><span>Xác thực nước đi, chiếu tướng & đồng hồ thời gian thực</span></div><div class="hero-pillar"><strong>Kỳ Đài Trực Tuyến</strong><span>Phòng đấu công khai/riêng tư, lời mời & Video 1–1</span></div><div class="hero-pillar"><strong>Đối Đầu AI</strong><span>Thuật toán Minimax / Alpha-Beta 3 cấp độ sâu</span></div></div></div><div><p class="eyebrow">THẾ CỜ KHAI CUỘC · 32 QUÂN KHẮC CHÌM</p><chess-board :interactive="true" :allow-any-target="false" player-side="Local" /></div></section>`,
  login: `<div class="auth-split"><aside class="auth-showcase"><div><div class="auth-seal">象</div><h2>Trở lại<br>Kỳ Đài.</h2><p>Đăng nhập để vào sảnh thi đấu, nhận lời mời từ bằng hữu và tiếp tục những ván cờ đang chờ.</p></div><span class="imperial-badge gold">Bảo mật JWT &amp; HttpOnly Session</span></aside><section class="card"><p class="eyebrow">CHÀO MỪNG TRỞ LẠI</p><h1>Đăng nhập</h1><p v-if="registered" class="success">Tài khoản đã tạo. Bạn có thể đăng nhập.</p><form @submit.prevent="login"><label>Tên đăng nhập hoặc email<input v-model="form.identifier" required autocomplete="username" placeholder="Nhập username hoặc email" /></label><label>Mật khẩu<input v-model="form.password" type="password" required autocomplete="current-password" placeholder="Nhập mật khẩu" /></label><label class="check"><input v-model="form.rememberMe" type="checkbox" /> Ghi nhớ phiên đăng nhập (7 ngày)</label><p v-if="error" class="error" role="alert">{{ error }}</p><button class="button" :disabled="loading">{{ loading ? 'Đang đăng nhập…' : 'Đăng nhập vào Kỳ Đài' }}</button></form><p>Chưa có tài khoản? <a href="/Account/Register">Đăng ký ngay</a></p></section></div>`,
  register: `<div class="auth-split"><aside class="auth-showcase"><div><div class="auth-seal">帥</div><h2>Ghi danh<br>Kỳ Thủ.</h2><p>Khởi tạo định danh của bạn trên Kỳ Đài để lưu trữ lịch sử đối đầu, thống kê thắng thua và thách đấu trực tuyến.</p></div><span class="imperial-badge gold">Miễn phí · Lưu trữ ván đấu tự động</span></aside><section class="card"><p class="eyebrow">BẮT ĐẦU HÀNH TRÌNH</p><h1>Tạo tài khoản</h1><form @submit.prevent="register"><label>Tên đăng nhập<input v-model="form.username" required minlength="3" maxlength="64" autocomplete="username" placeholder="Từ 3 đến 64 ký tự" /></label><label>Email<input v-model="form.email" type="email" required maxlength="254" autocomplete="email" placeholder="email@example.com" /></label><label>Mật khẩu<input v-model="form.password" type="password" required minlength="10" maxlength="256" autocomplete="new-password" placeholder="Tối thiểu 10 ký tự" /></label><label>Nhập lại mật khẩu<input v-model="form.confirmPassword" type="password" required minlength="10" autocomplete="new-password" placeholder="Nhập lại mật khẩu" /></label><p v-if="error" class="error" role="alert">{{ error }}</p><button class="button" :disabled="loading">{{ loading ? 'Đang tạo tài khoản…' : 'Đăng ký tài khoản' }}</button></form><p>Đã có tài khoản? <a href="/Account/Login">Đăng nhập</a></p></section></div>`,
  profile: `<div class="profile-shell"><section class="card"><p class="eyebrow">DANH THIẾP KỲ THỦ</p><div class="profile-banner"><div class="profile-monogram">{{ (user?.displayName || '象').slice(0, 1).toUpperCase() }}</div><div><h1>{{ user?.displayName }}</h1><p style="margin:0;color:var(--text-sand)">{{ user?.email }} · @{{ user?.username }}</p></div></div><form @submit.prevent="updateProfile"><label>Tên hiển thị trên bàn cờ<input v-model="form.displayName" required minlength="2" maxlength="80" /></label><p v-if="notice" class="success">{{ notice }}</p><p v-if="error" class="error">{{ error }}</p><button class="button">Lưu thay đổi</button></form></section><section class="card"><p class="eyebrow">TRUNG TÂM BẢO MẬT</p><h2>Đổi mật khẩu</h2><form @submit.prevent="changePassword"><label>Mật khẩu hiện tại<input v-model="form.currentPassword" type="password" required /></label><label>Mật khẩu mới<input v-model="form.newPassword" type="password" minlength="10" required /></label><label>Nhập lại mật khẩu mới<input v-model="form.confirmNewPassword" type="password" minlength="10" required /></label><button class="button">Cập nhật mật khẩu</button></form><hr/><div class="button-row"><button class="button secondary" @click="logout">Đăng xuất</button><button class="button danger" @click="logoutAll">Đăng xuất mọi thiết bị</button></div></section></div>`,
  lobby: `<div class="lobby-grid">
    <section class="card dashboard"><p class="eyebrow">KỲ ĐÀI TRỰC TUYẾN</p><h1>Chào {{ user?.displayName }}</h1><p style="color:var(--text-sand);margin-top:0">Chọn chế độ thi đấu hoặc mở bàn cờ mới.</p>
      <div class="stats"><div><strong>{{ stats?.wins ?? '—' }}</strong><span>Thắng</span></div><div><strong>{{ stats?.losses ?? '—' }}</strong><span>Thua</span></div><div><strong>{{ stats?.played ?? '—' }}</strong><span>Đã chơi</span></div></div>
      <div class="mode-cards">
        <button class="mode-card-btn" @click="showCreateRoom = !showCreateRoom"><div><strong>Tạo phòng trực tuyến</strong><small>Mở bàn công khai hoặc riêng tư mời bạn bè</small></div><span class="imperial-badge gold">+ Tạo</span></button>
        <form v-if="showCreateRoom" @submit.prevent="createRoom" style="padding:14px;border-radius:12px;background:var(--bg-abyss);border:1px solid var(--border-gold)"><label>Tên phòng đấu<input v-model.trim="roomName" minlength="3" maxlength="80" required placeholder="Ví dụ: Kỳ đài cuối tuần" /></label><label class="check"><input v-model="roomPrivate" type="checkbox" /> Phòng riêng tư (chỉ vào bằng mã/lời mời)</label><button class="button" :disabled="loading">Xác nhận tạo phòng</button></form>
        <button class="mode-card-btn" @click="showJoinModal = true"><div><strong>Vào phòng bằng mã 6 ký tự</strong><small>Nhập mã phòng do chủ bàn cung cấp</small></div><span class="imperial-badge">Mã số</span></button>
        <button class="mode-card-btn" @click="createLocalGame" :disabled="loading"><div><strong>Đấu 2 người tại máy này</strong><small>Luân phiên đi quân trên cùng một bàn cờ</small></div><span class="imperial-badge">Local</span></button>
        <a class="mode-card-btn" href="/AiGame/Index" style="text-decoration:none"><div><strong>Luyện cờ với Máy (AI)</strong><small>Đối đầu thuật toán Alpha-Beta 3 cấp độ</small></div><span class="imperial-badge jade">AI</span></a>
      </div>
      <p v-if="notice" class="success" role="status">{{ notice }}</p><p v-if="error" class="error" role="alert">{{ error }}</p><button class="link-button" @click="logout">Đăng xuất tài khoản</button>
    </section>
    <div v-if="showJoinModal" class="modal-backdrop" role="presentation" @click.self="showJoinModal = false"><section class="room-modal" role="dialog" aria-modal="true" aria-labelledby="join-room-title"><button class="modal-close" aria-label="Đóng" @click="showJoinModal = false">×</button><p class="eyebrow">THAM GIA PHÒNG</p><h2 id="join-room-title">Nhập mã phòng</h2><form @submit.prevent="joinRoom"><label>Mã gồm 6 ký tự<input v-model.trim="roomCode" maxlength="6" minlength="6" required autofocus autocomplete="off" placeholder="Ví dụ: K4D8XM" /></label><p v-if="error" class="error" role="alert">{{ error }}</p><button class="button" :disabled="loading || roomCode.length !== 6">{{ loading ? 'Đang tìm phòng…' : 'Tham gia phòng' }}</button></form></section></div>
    <section class="card room-browser"><div class="room-header-bar" style="border:0;padding-bottom:6px"><div><p class="eyebrow">SẢNH PHÒNG CÔNG KHAI</p><h2 style="margin:4px 0">Chọn bàn đối kháng</h2></div><span class="imperial-badge jade">{{ realtimeStatus }}</span></div>
      <form class="room-filters" @submit.prevent="loadPublicRooms(1)"><label>Tìm theo tên hoặc mã<input v-model.trim="roomSearch" maxlength="80" placeholder="Nhập tên phòng hoặc mã..." /></label><label>Trạng thái<select v-model="roomStatus"><option value="Waiting">Đang chờ đối thủ</option><option value="InGame">Đang thi đấu</option><option value="Closed">Đã đóng</option></select></label><button class="button secondary">Lọc bàn</button></form>
      <p v-if="roomsLoading">Đang tải danh sách bàn cờ…</p><p v-else-if="rooms.length === 0" style="padding:28px;text-align:center;color:var(--text-sand);border:1px dashed var(--border-subtle);border-radius:14px">Chưa có bàn đấu phù hợp. Hãy nhấn <strong>Tạo phòng trực tuyến</strong> ở cột trái để mở bàn đầu tiên!</p>
      <div class="room-table-grid"><article v-for="item in rooms" :key="item.id" class="room-card"><div><div style="display:flex;justify-content:space-between;align-items:center;gap:8px"><strong>{{ item.name }}</strong><span class="imperial-badge gold">#{{ item.code }}</span></div><small>Chủ bàn: {{ item.members.find(m => m.userId === item.ownerId)?.displayName || 'Kỳ thủ' }} · {{ item.members.length }}/{{ item.maxPlayers }} người</small><div class="room-seat-badges"><span class="seat-pill red">帥 Đỏ: {{ item.members.find(m => m.side === 'Red')?.displayName || 'Trống' }}</span><span class="seat-pill black">將 Đen: {{ item.members.find(m => m.side === 'Black')?.displayName || 'Trống' }}</span></div></div><button class="button" @click="joinPublicRoom(item)" :disabled="loading || item.status !== 'Waiting'">{{ item.status === 'Waiting' ? 'Ngồi vào bàn' : 'Đang đấu' }}</button></article></div>
      <div class="pagination"><button class="button secondary" @click="loadPublicRooms(roomPage - 1)" :disabled="roomPage <= 1 || roomsLoading">← Trang trước</button><span>Trang {{ roomPage }} · Tổng {{ roomTotal }} phòng</span><button class="button secondary" @click="loadPublicRooms(roomPage + 1)" :disabled="roomPage * 12 >= roomTotal || roomsLoading">Trang sau →</button></div>
    </section>
    <section class="card invitation-inbox"><p class="eyebrow">HỘP THƯ KỲ ĐÀI</p><h2>Lời mời thi đấu ({{ invitations.length }})</h2><p v-if="invitations.length === 0" style="color:var(--text-sand)">Hiện chưa có lời mời nào đang chờ.</p><article v-for="invitation in invitations" :key="invitation.id" class="room-card"><div><strong>{{ invitation.inviterUsername }} mời bạn vào bàn</strong><small>{{ invitation.roomName }} · Mã {{ invitation.roomCode }}</small><small>Hết hạn: {{ new Date(invitation.expiresAt).toLocaleString('vi-VN') }}</small></div><div class="button-row"><button class="button" @click="respondInvitation(invitation, true)" :disabled="loading">Chấp nhận</button><button class="button secondary" @click="respondInvitation(invitation, false)" :disabled="loading">Từ chối</button></div></article>
    </section>
  </div>`,
  room: `<section class="card room-waiting"><div class="room-header-bar"><div><p class="eyebrow">PHÒNG CHỜ THI ĐẤU · {{ realtimeStatus }}</p><h1>{{ room ? room.name : 'Đang tải phòng…' }}</h1><p v-if="room" style="color:var(--text-sand);margin:4px 0 0">Trạng thái: <strong>{{ room.status }}</strong> · {{ room.members.length }}/{{ room.maxPlayers }} kỳ thủ · Phiên #{{ room.version }}</p></div><div v-if="room" style="display:flex;gap:10px;align-items:center;flex-wrap:wrap"><span class="room-code-badge">{{ room.code }}</span><button class="button secondary" @click="copyCode">Sao chép mã</button><button v-if="room?.status === 'Waiting'" class="button secondary" @click="showInviteModal = true">+ Mời kỳ thủ</button></div></div>
    <div v-if="room" class="player-slots"><article v-for="side in ['Red','Black']" :key="side" class="player-slot" :class="side === 'Red' ? 'red-seat' : 'black-seat'"><div><h2><span class="player-mark" :class="side === 'Red' ? 'red-mark' : 'black-mark'">{{ side === 'Red' ? '帥' : '將' }}</span> Phe {{ side === 'Red' ? 'Đỏ (Đi tiên)' : 'Đen (Đi hậu)' }}</h2><template v-if="room.members.find(m => m.side === side)"><strong style="font-size:18px;color:var(--gold-bright)">{{ room.members.find(m => m.side === side).displayName }}</strong><small>{{ room.members.find(m => m.side === side).userId === room.ownerId ? 'Chủ phòng' : 'Kỳ thủ' }} · {{ room.members.find(m => m.side === side).isReady ? '✓ Đã sẵn sàng' : 'Đang chuẩn bị' }}</small></template><small v-else>Ghế đang trống — mời kỳ thủ hoặc chọn ngồi vào ghế này</small></div><button v-if="room.status === 'Waiting' && !room.members.find(m => m.side === side)" class="button secondary" @click="changeSide(side)" :disabled="loading">Chọn cầm quân {{ side === 'Red' ? 'Đỏ' : 'Đen' }}</button></article></div>
    <div v-if="room?.status === 'Waiting'" class="room-actions"><button class="button" @click="toggleReady" :disabled="loading || !myMembership?.side">{{ myMembership?.isReady ? 'Hủy sẵn sàng' : 'Sẵn sàng thi đấu' }}</button><button v-if="isOwner" class="button" @click="startRoom" :disabled="loading || !canStart">Khai cuộc (Bắt đầu trận)</button><button class="button secondary" @click="leaveRoom" :disabled="loading">Rời bàn</button><a class="button secondary" href="/Lobby">Về sảnh</a></div>
    <p v-if="isOwner && room?.status === 'Waiting'" style="color:var(--text-sand);font-size:13px">Chủ phòng có thể bắt đầu trận đấu ngay khi cả hai bên Đỏ &amp; Đen đã nhấn Sẵn sàng.</p><a v-if="room?.gameId" class="button" :href="'/games/' + room.gameId">Vào bàn cờ đang diễn ra →</a>
    <div v-if="showInviteModal" class="modal-backdrop" role="presentation" @click.self="showInviteModal = false"><section class="room-modal" role="dialog" aria-modal="true" aria-labelledby="invite-player-title"><button class="modal-close" aria-label="Đóng" @click="showInviteModal = false">×</button><p class="eyebrow">LỜI MỜI KỲ THỦ</p><h2 id="invite-player-title">Mời người chơi vào phòng</h2><form class="invite-form" @submit.prevent="sendInvitation"><label>Tên đăng nhập của bạn bè<input v-model.trim="inviteUsername" minlength="3" maxlength="64" required autofocus placeholder="Nhập username..." /></label><p v-if="error" class="error" role="alert">{{ error }}</p><button class="button" :disabled="loading">{{ loading ? 'Đang gửi…' : 'Gửi lời mời' }}</button></form></section></div>
    <p v-if="notice" class="success">{{ notice }}</p><p v-if="error" class="error" role="alert">{{ error }}</p>
  </section>`,
  game: `<section :class="game?.mode === 'Online' ? 'online-game-shell' : 'online-game-shell game-card'"><header class="online-game-header"><div><p class="eyebrow">KỲ ĐÀI · VÁN ĐẤU {{ game?.mode === 'Local' ? 'HAI NGƯỜI TẠI MÁY' : game?.mode === 'Online' ? 'TRỰC TUYẾN' : 'XEM TRƯỚC' }}</p><h1>Đối diện thế cờ.</h1><p v-if="game">Lượt đi: <strong>{{ game.currentTurn === 'Red' ? 'Đỏ (帥)' : 'Đen (將)' }}</strong> <span v-if="game.playerSide !== 'Local'">· Bạn cầm quân {{ game.playerSide === 'Red' ? 'Đỏ' : 'Đen' }}</span> · Nước thứ {{ game.ply }}</p></div><div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap"><span v-if="game?.mode === 'Online'" class="connection-indicator" :class="connectionStatus.toLowerCase()">{{ connectionStatusLabel }}</span><button v-if="game?.mode === 'Local'" class="button secondary small-button" @click="restartLocalGame" :disabled="loading">Ván mới</button><a v-if="game?.roomId" class="button secondary small-button" :href="'/Room/Index/' + game.roomId">Về phòng</a><a v-else class="button secondary small-button" href="/Lobby">Về sảnh</a></div></header>
    <div v-if="game?.mode === 'Online'" class="online-players"><article class="online-player" :class="{ active: game.currentTurn === 'Red' }"><div class="player-mark red-mark">帥</div><div><strong>{{ redPlayerName }}</strong><small style="display:block;color:var(--text-sand)">Phe Đỏ · Đi tiên</small></div><output class="player-clock">{{ redClockText }}</output></article><article class="online-player" :class="{ active: game.currentTurn === 'Black' }"><div class="player-mark black-mark">將</div><div><strong>{{ blackPlayerName }}</strong><small style="display:block;color:var(--text-sand)">Phe Đen · Đi hậu</small></div><output class="player-clock">{{ blackClockText }}</output></article></div>
    <p v-if="game?.mode === 'Online' && game.status === 'InProgress'" class="turn-status">{{ game.currentTurn === game.playerSide ? 'Đến lượt bạn đi quân' : 'Đang chờ đối thủ suy nghĩ' }} · {{ opponentConnectionLabel }}</p>
    <div v-if="connectionStatus === 'Reconnecting' || connectionStatus === 'Disconnected'" class="reconnect-overlay" role="status">Mất kết nối tới ván đấu. Đang khôi phục trạng thái mới nhất…</div>
    <p v-if="game?.isInCheck" class="check-alert">將軍 · CHIẾU TƯỚNG! Phe {{ game.currentTurn === 'Red' ? 'Đỏ' : 'Đen' }} phải hóa giải nước chiếu.</p><p v-if="loading || pendingMoveId" class="success">Đang chờ máy chủ xác nhận nước đi…</p>
    <div class="game-interaction-grid"><div class="game-board-area"><chess-board v-if="game" :key="game.version" :fen="game.currentFen" :interactive="canInteract" :player-side="game.mode === 'Local' ? game.currentTurn : game.playerSide" :show-turn-errors="game.mode === 'Local'" :legal-targets="legalTargets" :last-move="moveHistory[moveHistory.length - 1]" @select-piece="loadLegalMoves" @clear-selection="clearLegalMoves" @wrong-turn="showLocalTurnMessage" @move-selected="applyMove" /><chess-board v-else-if="!gameId" />
      <div v-if="game?.mode === 'Online' && game.status === 'InProgress'" class="online-game-actions"><button class="button danger" @click="showResignDialog = true" :disabled="loading || !!pendingMoveId">Nhận thua (Đầu hàng)</button></div></div>
      <aside class="game-interactions">
        <section v-if="game?.mode === 'Online'" class="call-panel"><p class="eyebrow">ĐÀM THOẠI VIDEO 1–1</p><h2>{{ callLabel }}</h2><p v-if="callNotice" class="success">{{ callNotice }}</p><p v-if="callError" class="error" role="alert">{{ callError }}</p><div class="call-controls"><button v-if="!activeCall && !incomingCall" class="button secondary small-button" @click="startVideoCall" :disabled="connectionStatus !== 'Connected' || game?.status !== 'InProgress'">Gọi video</button><button v-if="incomingCall" class="button small-button" @click="acceptVideoCall">Nhận cuộc gọi</button><button v-if="incomingCall" class="button secondary small-button" @click="rejectVideoCall">Từ chối</button><button v-if="activeCall" class="button danger small-button" @click="endVideoCall">Kết thúc</button><button v-if="localStream" class="button secondary small-button" @click="toggleCallMic">{{ callMicEnabled ? 'Tắt mic' : 'Bật mic' }}</button><button v-if="localStream" class="button secondary small-button" @click="toggleCallCamera">{{ callCameraEnabled ? 'Tắt camera' : 'Bật camera' }}</button></div><div v-if="localStream || remoteStream" class="call-videos"><video ref="remoteVideo" autoplay playsinline></video><video ref="localVideo" autoplay muted playsinline></video></div></section>
        <section v-if="game?.mode === 'Online'" class="chat-panel"><header><div><p class="eyebrow">TRÒ CHUYỆN TRONG VÁN</p><h2>Đàm đạo</h2></div><button v-if="chatHasMore" class="button secondary small-button" @click="loadOlderChat">Tin cũ</button></header><div ref="chatList" class="chat-list" aria-live="polite"><article v-for="message in chatMessages" :key="message.id" class="chat-message" :class="{ mine: message.senderUserId === user?.id }"><strong>{{ message.senderUserId === user?.id ? 'Bạn' : message.senderDisplayName }}</strong><p>{{ message.content }}</p><time>{{ new Date(message.createdAtUtc).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) }}</time></article><p v-if="!chatMessages.length" class="chat-empty">Chưa có tin nhắn. Hãy gửi lời chào đối thủ!</p></div><form class="chat-compose" @submit.prevent="sendChatMessage"><label class="sr-only" for="chat-input">Tin nhắn</label><input id="chat-input" v-model="chatDraft" maxlength="1000" autocomplete="off" placeholder="Nhập tin nhắn…" :disabled="connectionStatus !== 'Connected'" /><button class="button small-button" :disabled="!chatDraft.trim() || connectionStatus !== 'Connected'">Gửi</button></form><p v-if="chatError" class="error" role="alert">{{ chatError }}</p></section>
        <section class="move-log" style="margin-top:0"><h2>Biên bản nước đi ({{ moveHistory.length }})</h2><ol v-if="moveHistory.length"><li v-for="move in moveHistory" :key="move.ply">{{ move.ply }}. {{ move.piece }} {{ move.fromSquare }} → {{ move.toSquare }}<span v-if="move.capturedPiece"> · Ăn {{ move.capturedPiece }}</span></li></ol><p v-else style="color:var(--text-sand);font-size:13px">Chưa có nước đi nào được thực hiện.</p><p v-if="moveHistory.length" style="font-size:12px;color:var(--gold-bright);margin-bottom:0">Quân đã ăn: {{ moveHistory.filter(move => move.capturedPiece).map(move => move.capturedPiece).join(' ') || 'Chưa có' }}</p></section>
      </aside></div>
    <p v-if="game?.status === 'Finished'" class="game-finished-line">Ván đấu đã kết thúc · {{ resultLabel }}</p>
    <p v-if="notice" class="success">{{ notice }}</p><p v-if="error" class="error" role="alert">{{ error }}</p>
    <div v-if="showResignDialog" class="modal-backdrop" role="presentation" @click.self="showResignDialog = false"><section class="room-modal" role="dialog" aria-modal="true" aria-labelledby="resign-title"><p class="eyebrow">XÁC NHẬN NHẬN THUA</p><h2 id="resign-title">Bạn muốn đầu hàng ván này?</h2><p style="color:var(--text-sand)">Đối thủ sẽ được xử thắng ngay lập tức.</p><div class="button-row"><button class="button danger" @click="confirmResign" :disabled="loading">Xác nhận đầu hàng</button><button class="button secondary" @click="showResignDialog = false">Tiếp tục chiến đấu</button></div></section></div>
    <div v-if="game?.mode === 'Online' && game.status === 'Finished'" class="modal-backdrop result-backdrop"><section class="room-modal result-modal" role="dialog" aria-modal="true" aria-labelledby="result-title"><p class="eyebrow">KẾT QUẢ VÁN ĐẤU</p><h2 id="result-title">{{ resultLabel }}</h2><p style="color:var(--text-sand)">{{ resultReasonLabel }}</p><div class="button-row" style="justify-content:center"><a v-if="game?.roomId" class="button secondary" :href="'/Room/Index/' + game.roomId">Về phòng chờ</a><a class="button" href="/Lobby">Về sảnh chơi</a></div></section></div></section>`,
  'ai-lobby': `<section class="card ai-settings-page"><p class="eyebrow">KỲ ĐÀI · ĐỐI ĐẦU TRÍ TUỆ NHÂN TẠO</p><h1>Chọn thế trận.</h1><p>Máy chủ tìm nước đi bằng thuật toán Minimax cắt tỉa Alpha-Beta kết hợp Quiescence Search và lưu trạng thái ván đấu vào cơ sở dữ liệu.</p><form @submit.prevent="createAiGame"><fieldset><legend>Cấp độ Kỳ Sư AI</legend><label class="ai-choice"><input v-model="aiDifficulty" type="radio" value="Easy"/><span><strong>Sơ Cấp (Dễ)</strong><small>Phản hồi nhanh, phù hợp làm quen thế trận</small></span></label><label class="ai-choice"><input v-model="aiDifficulty" type="radio" value="Medium"/><span><strong>Trung Cấp (Vừa)</strong><small>Cân bằng giữa độ sâu tính toán và tốc độ</small></span></label><label class="ai-choice"><input v-model="aiDifficulty" type="radio" value="Hard"/><span><strong>Đại Sư (Khó)</strong><small>Tìm kiếm sâu tối đa trong giới hạn 5 giây</small></span></label></fieldset><fieldset><legend>Chọn quân cầm cờ</legend><label class="ai-choice inline"><input v-model="aiHumanSide" type="radio" value="Red"/> <span class="player-mark red-mark" style="width:32px;height:32px;font-size:15px">帥</span> Cầm quân Đỏ (Đi tiên)</label><label class="ai-choice inline"><input v-model="aiHumanSide" type="radio" value="Black"/> <span class="player-mark black-mark" style="width:32px;height:32px;font-size:15px">將</span> Cầm quân Đen (Đi hậu)</label><p v-if="aiHumanSide === 'Black'" class="ai-hint" style="width:100%;margin:4px 0 0">Máy sẽ khai cuộc trước bằng bên Đỏ ngay khi tạo bàn.</p></fieldset><p v-if="error" class="error" role="alert">{{ error }}</p><div class="button-row"><a class="button secondary" href="/Lobby">← Về sảnh</a><button class="button" :disabled="loading">{{ loading ? 'Đang khởi tạo bàn cờ…' : 'Bắt đầu ván đấu' }}</button></div></form></section>`,
  'ai-game': `<section class="ai-play-page"><header class="ai-page-header"><div><p class="eyebrow">ĐỐI ĐẦU VỚI MÁY · CẤP ĐỘ {{ aiDifficultyLabel(game?.difficulty).toUpperCase() }}</p><h1>Đối diện thế cờ.</h1></div><p class="ai-turn" :class="{ thinking: isAiThinking }" role="status">{{ aiTurnLabel }}</p></header><p v-if="game?.isCurrentTurnInCheck" class="check-alert">將軍 · CHIẾU TƯỚNG! {{ game.currentTurn === 'Red' ? 'Đỏ' : 'Đen' }} phải thoát chiếu.</p><div class="ai-game-grid"><div class="ai-board-card"><div class="ai-player-strip"><div style="display:flex;align-items:center;gap:10px"><span class="player-mark black-mark" style="width:34px;height:34px;font-size:15px">將</span><strong>Đen {{ game?.aiSide === 'Black' ? '· Kỳ Sư AI' : game?.humanSide === 'Black' ? '· Bạn' : '' }}</strong></div><span>{{ game?.currentTurn === 'Black' && !aiGameFinished ? 'Đang đi' : 'Chờ lượt' }}</span></div><chess-board v-if="game" :key="game.version" :fen="game.fen" :interactive="canInteract" :player-side="game.humanSide" :allow-any-target="true" :legal-targets="[]" :last-move="moveHistory[moveHistory.length - 1]" @move-selected="submitAiMove"/><div class="ai-player-strip"><div style="display:flex;align-items:center;gap:10px"><span class="player-mark red-mark" style="width:34px;height:34px;font-size:15px">帥</span><strong>Đỏ {{ game?.aiSide === 'Red' ? '· Kỳ Sư AI' : game?.humanSide === 'Red' ? '· Bạn' : '' }}</strong></div><span>{{ game?.currentTurn === 'Red' && !aiGameFinished ? 'Đang đi' : 'Chờ lượt' }}</span></div><p v-if="isAiThinking" class="ai-hint">Máy đang phân tích cây trò chơi. Trạng thái ván đấu được bảo toàn trên máy chủ.</p><p v-else-if="game?.error" class="error">{{ game.error }}</p><p v-else-if="error" class="error" role="alert">{{ error }}</p></div><aside class="ai-history-card"><p class="eyebrow">PHÂN TÍCH THUẬT TOÁN</p><h2>{{ aiTurnLabel }}</h2><p v-if="aiAnalysis?.completedDepth" style="color:var(--gold-bright);font-size:13px">Độ sâu {{ aiAnalysis.completedDepth }} · {{ aiAnalysis.nodesSearched.toLocaleString('vi-VN') }} nút · {{ aiAnalysis.elapsedMs }} ms</p><p v-else style="color:var(--text-sand);font-size:13px">Độ khó: {{ aiDifficultyLabel(game?.difficulty) }}</p><div class="ai-history-heading"><h2>Biên bản nước đi</h2><span>{{ moveHistory.length }} nước</span></div><ol class="ai-move-history"><li v-for="move in moveHistory" :key="move.ply"><span>#{{ move.ply }}</span><strong>{{ move.isAiMove ? 'Máy' : 'Bạn' }}</strong><code>{{ displayAiSquare(move.fromSquare) }} → {{ displayAiSquare(move.toSquare) }}</code></li><li v-if="!moveHistory.length" class="muted">{{ isAiThinking ? 'Máy đang suy nghĩ nước khai cuộc…' : 'Hãy chọn một quân cờ để khai cuộc.' }}</li></ol><div class="ai-controls"><button class="button danger" @click="resignAiGame" :disabled="loading || aiGameFinished">Đầu hàng</button><button class="button secondary" @click="restartAiGame" :disabled="loading || !game">Đánh lại</button></div><p v-if="game?.winnerSide" class="success">{{ game.winnerSide === game.humanSide ? 'Bạn đã chiến thắng!' : 'Kỳ Sư AI thắng.' }} · {{ game.resultReason || 'Kết thúc' }}</p><p v-if="error" class="error" role="alert">{{ error }}</p><a class="button secondary" href="/AiGame/Index">Đổi độ khó / Tạo ván mới</a></aside></div></section>`,
};

const pieceNames = { k: '將', a: '士', b: '象', n: '馬', r: '車', c: '炮', p: '卒', K: '帥', A: '仕', B: '相', N: '傌', R: '俥', C: '砲', P: '兵' };
function piecesFromFen(fen) {
  if (!fen) return initialPieces;
  const pieces = [];
  fen.split(' ')[0].split('/').forEach((row, rank) => {
    let file = 0;
    for (const symbol of row) {
      if (/\d/.test(symbol)) { file += Number(symbol); continue; }
      pieces.push({ name: pieceNames[symbol], file, rank, side: symbol === symbol.toUpperCase() ? 'red' : 'black' }); file++;
    }
  });
  return pieces;
}

const ChessBoard = {
  props: { fen: String, interactive: Boolean, playerSide: String, showTurnErrors: { type: Boolean, default: false }, allowAnyTarget: { type: Boolean, default: false }, legalTargets: { type: Array, default: () => [] }, lastMove: Object },
  data: () => ({ flipped: false, selected: null }),
  computed: {
    pieces() { return piecesFromFen(this.fen); },
    cells() {
      const from = this.lastMove ? { file: this.lastMove.fromSquare.charCodeAt(0) - 97, rank: Number(this.lastMove.fromSquare.slice(1)) } : null;
      const to = this.lastMove ? { file: this.lastMove.toSquare.charCodeAt(0) - 97, rank: Number(this.lastMove.toSquare.slice(1)) } : null;
      return Array.from({ length: 90 }, (_, index) => {
        const file = index % 9; const rank = Math.floor(index / 9);
        return { file, rank, piece: this.pieces.find(item => item.file === file && item.rank === rank), lastFrom: from?.file === file && from?.rank === rank, lastTo: to?.file === file && to?.rank === rank };
      });
    },
  },
  methods: {
    choose(cell) {
      if (!this.interactive) return;
      const target = this.legalTargets.some(move => move.file === cell.file && move.rank === cell.rank);
      const ownPiece = cell.piece && (this.playerSide === 'Local' || (cell.piece.side === 'red' ? 'Red' : 'Black') === this.playerSide);
      if (this.selected && (target || (this.allowAnyTarget && !ownPiece && (this.selected.file !== cell.file || this.selected.rank !== cell.rank)))) { this.$emit('move-selected', { from: this.selected, to: { file: cell.file, rank: cell.rank } }); this.selected = null; return; }
      if (ownPiece) {
        this.selected = { file: cell.file, rank: cell.rank }; this.$emit('select-piece', this.selected);
      } else {
        const hadSelection = !!this.selected;
        this.selected = null; this.$emit('clear-selection');
        if (cell.piece && this.showTurnErrors && !hadSelection) this.$emit('wrong-turn');
      }
    },
  },
  template: `<div class="board-wrap"><div class="board-toolbar-row"><span class="imperial-badge gold">楚河 · 漢界</span><button type="button" class="flip" @click="flipped = !flipped">{{ flipped ? 'Đưa Đỏ xuống dưới' : 'Lật bàn cờ' }}</button></div><div class="board-grid" :class="{ flipped }"><svg class="board-lines" viewBox="0 0 800 900" preserveAspectRatio="none" aria-hidden="true"><rect x="28" y="28" width="744" height="844" fill="none" stroke="#7a1c1c" stroke-width="6"/><rect x="40" y="40" width="720" height="820" fill="none" stroke="#7a1c1c" stroke-width="3"/><path d="M40 40V860 M130 40V404 M130 496V860 M220 40V404 M220 496V860 M310 40V404 M310 496V860 M400 40V404 M400 496V860 M490 40V404 M490 496V860 M580 40V404 M580 496V860 M670 40V404 M670 496V860 M760 40V860 M40 40H760 M40 131H760 M40 222H760 M40 313H760 M40 404H760 M40 496H760 M40 587H760 M40 678H760 M40 769H760 M40 860H760 M310 40L490 222 M490 40L310 222 M310 678L490 860 M490 678L310 860" fill="none" stroke="#7a1c1c" stroke-width="2.6"/><g fill="none" stroke="#7a1c1c" stroke-width="2.4"><path d="M112 204h12v-12 M148 204h-12v-12 M112 240h12v12 M148 240h-12v12 M652 204h12v-12 M688 204h-12v-12 M652 240h12v12 M688 240h-12v12"/><path d="M112 660h12v-12 M148 660h-12v-12 M112 696h12v12 M148 696h-12v12 M652 660h12v-12 M688 660h-12v-12 M652 696h12v12 M688 696h-12v12"/><path d="M46 298v10h10 M46 328v-10h10 M754 298v10h-10 M754 328v-10h-10 M46 572v10h10 M46 602v-10h10 M754 572v10h-10 M754 602v-10h-10"/><path d="M204 298h10v-10 M236 298h-10v-10 M204 328h10v10 M236 328h-10v10 M384 298h10v-10 M416 298h-10v-10 M384 328h10v10 M416 328h-10v10 M564 298h10v-10 M596 298h-10v-10 M564 328h10v10 M596 328h-10v10"/><path d="M204 572h10v-10 M236 572h-10v-10 M204 602h10v10 M236 602h-10v10 M384 572h10v-10 M416 572h-10v-10 M384 602h10v10 M416 602h-10v10 M564 572h10v-10 M596 572h-10v-10 M564 602h10v10 M596 602h-10v10"/></g></svg><div class="river"><span>楚河</span><span>漢界</span></div><button v-for="cell in cells" :key="cell.rank * 9 + cell.file" class="board-square" :class="{ selected: selected?.file === cell.file && selected?.rank === cell.rank, legal: legalTargets.some(target => target.file === cell.file && target.rank === cell.rank), 'last-from': cell.lastFrom, 'last-to': cell.lastTo }" :style="{ left: (5 + cell.file * 11.25) + '%', top: (4.444 + cell.rank * 10.123) + '%' }" @click="choose(cell)" :aria-label="cell.piece?.name || ('Ô ' + cell.file + ',' + cell.rank)"><span v-if="cell.piece" class="piece" :class="cell.piece.side">{{ cell.piece.name }}</span></button></div><small v-if="interactive">Chọn quân cờ để xem nước đi hợp lệ, sau đó chọn điểm đến.</small><small v-else>Chọn quân cờ để làm nổi bật · Bàn cờ Tướng chuẩn 9×10.</small></div>`,
};

const root = document.querySelector('#app');
if (root) {
  const page = root.dataset.page;
  createApp({
    components: { ChessBoard },
    template: pageTemplates[page] || '<section class="card"><h1>Không tìm thấy trang</h1><a href="/">Về trang chủ</a></section>',
    data: () => ({ user: null, stats: null, room: null, game: null, gameHub: null, gameRetryTimer: null, clockTimer: null, clockNow: Date.now(), clockState: null, serverClockOffsetMs: 0, redPlayerName: 'Đỏ', blackPlayerName: 'Đen', connectionStatus: 'Connecting', opponentConnection: 'Unknown', pendingMoveId: null, pendingMoveRequest: null, showResignDialog: false, gameRetryAttempts: 0, rooms: [], invitations: [], moveHistory: [], legalTargets: [], chatMessages: [], chatDraft: '', chatError: '', chatHasMore: false, chatCursor: null, pendingChatRequest: null, incomingCall: null, activeCall: null, callNotice: '', callError: '', peerConnection: null, localStream: null, remoteStream: null, pendingIceCandidates: [], callMicEnabled: true, callCameraEnabled: true, offerSent: false, iceServers: null, roomCode: '', roomName: 'Phòng mới', roomPrivate: false, showCreateRoom: false, showJoinModal: false, showInviteModal: false, roomSearch: '', roomStatus: 'Waiting', roomPage: 1, roomTotal: 0, roomsLoading: false, inviteUsername: '', realtimeStatus: 'Đang kết nối', roomHub: null, roomRetryTimer: null, aiDifficulty: 'Medium', aiHumanSide: 'Red', aiAnalysis: null, aiPollTimer: null, aiRequestInFlight: false, isUnmounted: false, error: '', notice: '', loading: false, registered: new URLSearchParams(location.search).has('registered'), roomId: root.dataset.roomId, gameId: root.dataset.gameId, form: { identifier: '', password: '', rememberMe: true, username: '', email: '', confirmPassword: '', displayName: '', currentPassword: '', newPassword: '', confirmNewPassword: '' } }),
    computed: {
      myMembership() { return this.room?.members.find(member => member.userId === this.user?.id) || null; },
      isOwner() { return this.room?.ownerId === this.user?.id; },
      canStart() { return this.room?.members.length === 2 && this.room.members.every(member => member.side && member.isReady) && new Set(this.room.members.map(member => member.side)).size === 2; },
      canInteract() {
        if (!this.game || this.game.status !== 'InProgress' || this.loading || this.pendingMoveId) return false;
        if (this.game.mode === 'Local') return true;
        if (page === 'ai-game') return this.game.currentTurn === this.game.humanSide && this.game.aiTurnStatus === 'WaitingForHuman';
        return this.connectionStatus === 'Connected' && this.game.playerSide === this.game.currentTurn;
      },
      isAiThinking() { return this.game?.status === 'InProgress' && ['Queued', 'Thinking'].includes(this.game.aiTurnStatus); },
      aiGameFinished() { return !!this.game && this.game.status !== 'InProgress'; },
      aiTurnLabel() {
        if (!this.game) return 'Đang tải ván đấu…';
        if (this.game.winnerSide) return this.game.winnerSide === this.game.humanSide ? 'Bạn đã thắng!' : 'Máy đã thắng';
        if (this.aiGameFinished) return this.game.resultReason || 'Ván đấu đã kết thúc';
        if (this.isAiThinking) return 'Máy đang suy nghĩ…';
        return this.game.currentTurn === this.game.humanSide ? 'Đến lượt bạn' : 'Đang xử lý nước đi';
      },
      connectionStatusLabel() { return ({ Connected: 'Đã kết nối', Reconnecting: 'Đang kết nối lại', Disconnected: 'Mất kết nối', Connecting: 'Đang kết nối' })[this.connectionStatus] || 'Đang kết nối'; },
      opponentConnectionLabel() { return this.opponentConnection === 'Disconnected' ? 'Đối thủ mất kết nối · ván vẫn tiếp tục' : this.opponentConnection === 'Connected' ? 'Đối thủ đang trực tuyến' : 'Trạng thái kết nối đối thủ chưa rõ'; },
      redClockText() { return this.formatClock(this.clockRemaining('Red')); },
      blackClockText() { return this.formatClock(this.clockRemaining('Black')); },
      callLabel() { return this.incomingCall ? `${this.incomingCall.callerDisplayName} đang gọi` : this.activeCall ? `Cuộc gọi ${this.activeCall.status === 'Ringing' ? 'đang đổ chuông' : this.activeCall.status === 'Connected' ? 'đang diễn ra' : 'đang kết nối'}` : 'Chưa có cuộc gọi'; },
      resultLabel() {
        if (!this.game || this.game.resultReason === 'Draw' || this.game.resultReason === 'Stalemate' && !this.game.winnerId) return 'Ván đấu đã kết thúc';
        return this.game.winnerId === this.user?.id ? 'Bạn đã thắng!' : 'Bạn đã thua';
      },
      resultReasonLabel() { return ({ Checkmate: 'Chiếu bí', Resignation: 'Đối thủ đầu hàng', Timeout: 'Hết giờ', Stalemate: 'Đối thủ không còn nước đi hợp lệ' })[this.game?.resultReason] || 'Kết quả đã được máy chủ lưu'; },
    },
    async created() {
      try {
        const session = await api('/auth/refresh', 'POST', {});
        accessToken = session.accessToken;
        accessTokenExpiresAt = Date.now() + session.expiresIn * 1000;
        this.user = await api('/users/me');
        this.form.displayName = this.user.displayName;
      } catch {
        accessToken = null;
        if (['profile', 'lobby', 'room', 'game', 'ai-lobby', 'ai-game'].includes(page)) location.replace(`/Account/Login?returnUrl=${encodeURIComponent(location.pathname + location.search)}`);
        return;
      }
      document.querySelector('#nav-user').textContent = this.user.displayName;
      document.querySelector('#nav-user').hidden = false;
      document.querySelector('#login-link').hidden = true;
      if (['login', 'register'].includes(page)) { location.replace('/Lobby'); return; }
      try {
        if (page === 'lobby') {
          this.stats = await api('/users/me/stats');
          await Promise.all([this.loadPublicRooms(1), this.loadInvitations()]);
          await this.connectRoomHub();
        }
        if (page === 'room' && this.roomId) {
          this.room = await api(`/rooms/${encodeURIComponent(this.roomId)}`);
          await this.connectRoomHub();
        }
        if (page === 'game' && this.gameId) {
          this.game = await api(`/games/${encodeURIComponent(this.gameId)}`);
          this.moveHistory = await api(`/games/${encodeURIComponent(this.gameId)}/moves`);
          if (this.game.mode === 'Online') await this.connectGameHub();
          this.clockTimer = setInterval(() => { this.clockNow = Date.now(); }, 250);
        }
        if (page === 'ai-game' && this.gameId) await this.loadAiGame();
      } catch (error) { this.error = error.message; }
    },
    beforeUnmount() {
      this.isUnmounted = true;
      if (this.roomRetryTimer) clearTimeout(this.roomRetryTimer);
      if (this.gameRetryTimer) clearTimeout(this.gameRetryTimer);
      if (this.clockTimer) clearInterval(this.clockTimer);
      if (this.aiPollTimer) clearTimeout(this.aiPollTimer);
      this.cleanupCall();
      this.roomHub?.stop();
      this.gameHub?.stop();
    },
    methods: {
      async login() {
        this.loading = true; this.error = '';
        try { const session = await api('/auth/login', 'POST', this.form); accessToken = session.accessToken; accessTokenExpiresAt = Date.now() + session.expiresIn * 1000; location.assign('/Lobby'); }
        catch (error) { this.error = error.message; } finally { this.loading = false; }
      },
      async register() {
        this.loading = true; this.error = '';
        try { await api('/auth/register', 'POST', this.form); location.assign('/Account/Login?registered=1'); }
        catch (error) { this.error = error.message; } finally { this.loading = false; }
      },
      async updateProfile() {
        this.error = ''; this.notice = '';
        try { this.user = await api('/users/me', 'PATCH', { displayName: this.form.displayName }); this.notice = 'Đã lưu hồ sơ.'; }
        catch (error) { this.error = error.message; }
      },
      async changePassword() {
        this.error = '';
        try { await api('/users/me/password', 'PUT', this.form); accessToken = null; location.assign('/Account/Login'); }
        catch (error) { this.error = error.message; }
      },
      async createRoom() {
        this.loading = true; this.error = '';
        try { const room = await api('/rooms', 'POST', { name: this.roomName, isPrivate: this.roomPrivate }); location.assign(`/Room/Index/${encodeURIComponent(room.id)}`); }
        catch (error) { this.error = error.message; } finally { this.loading = false; }
      },
      async createLocalGame() {
        this.loading = true; this.error = '';
        try { const game = await api('/games/local', 'POST', {}); location.assign(`/Game/Index/${encodeURIComponent(game.id)}`); }
        catch (error) { this.error = error.message; } finally { this.loading = false; }
      },
      async createAiGame() {
        this.loading = true; this.error = '';
        try {
          const created = await api('/ai-games', 'POST', { difficulty: this.aiDifficulty, humanSide: this.aiHumanSide });
          location.assign(`/AiGame/Play/${encodeURIComponent(created.gameId)}`);
        } catch (error) { this.error = error.message; } finally { this.loading = false; }
      },
      async loadAiGame() {
        if (!this.gameId || this.aiRequestInFlight || this.isUnmounted) return;
        this.aiRequestInFlight = true;
        try {
          const [game, history, analysis] = await Promise.all([
            api(`/ai-games/${encodeURIComponent(this.gameId)}`),
            api(`/ai-games/${encodeURIComponent(this.gameId)}/moves`),
            api(`/ai-games/${encodeURIComponent(this.gameId)}/analysis-status`),
          ]);
          this.game = game;
          this.moveHistory = history.moves || [];
          this.aiAnalysis = analysis;
          this.error = '';
        } catch (error) { this.error = error.message; }
        finally {
          this.aiRequestInFlight = false;
          if (this.aiPollTimer) clearTimeout(this.aiPollTimer);
          if (!this.isUnmounted && this.isAiThinking) this.aiPollTimer = setTimeout(() => this.loadAiGame(), 1200);
        }
      },
      async submitAiMove(move) {
        if (!this.canInteract) return;
        this.loading = true; this.error = '';
        try {
          await api(`/ai-games/${encodeURIComponent(this.gameId)}/moves`, 'POST', {
            fromFile: move.from.file, fromRank: move.from.rank, toFile: move.to.file, toRank: move.to.rank,
            expectedVersion: this.game.version, clientMoveId: crypto.randomUUID(),
          });
          await this.loadAiGame();
        } catch (error) { this.error = error.message; await this.loadAiGame(); }
        finally { this.loading = false; }
      },
      async resignAiGame() {
        if (!this.game || this.aiGameFinished) return;
        this.loading = true; this.error = '';
        try { await api(`/ai-games/${encodeURIComponent(this.gameId)}/resign`, 'POST', { expectedVersion: this.game.version }); await this.loadAiGame(); }
        catch (error) { this.error = error.message; await this.loadAiGame(); }
        finally { this.loading = false; }
      },
      async restartAiGame() {
        if (!this.game) return;
        this.loading = true; this.error = '';
        try {
          const game = await api(`/ai-games/${encodeURIComponent(this.gameId)}/restart`, 'POST', { expectedVersion: this.game.version });
          location.assign(`/AiGame/Play/${encodeURIComponent(game.gameId)}`);
        } catch (error) { this.error = error.message; await this.loadAiGame(); }
        finally { this.loading = false; }
      },
      aiDifficultyLabel(value) { return ({ Easy: 'Dễ', Medium: 'Vừa', Hard: 'Khó' })[value] || 'Vừa'; },
      displayAiSquare(square) { return square ? `${square[0].toUpperCase()}${10 - Number(square.slice(1))}` : '—'; },
      async joinRoom() {
        this.loading = true; this.error = '';
        try { const room = await api('/rooms/join', 'POST', { code: this.roomCode }); location.assign(`/Room/Index/${encodeURIComponent(room.id)}`); }
        catch (error) { this.error = error.message; } finally { this.loading = false; }
      },
      async joinPublicRoom(room) {
        this.loading = true; this.error = '';
        try { const joined = await api('/rooms/join-public', 'POST', { roomId: room.id }); location.assign(`/Room/Index/${encodeURIComponent(joined.id)}`); }
        catch (error) { this.error = error.message; await this.loadPublicRooms(this.roomPage); } finally { this.loading = false; }
      },
      async loadPublicRooms(page = this.roomPage) {
        if (page < 1) return;
        this.roomsLoading = true;
        try {
          const query = new URLSearchParams({ search: this.roomSearch, status: this.roomStatus, page: String(page) });
          const result = await api(`/rooms?${query}`);
          this.rooms = result.items; this.roomPage = result.page; this.roomTotal = result.totalCount;
        } catch (error) { this.error = error.message; } finally { this.roomsLoading = false; }
      },
      async loadInvitations() {
        try { this.invitations = await api('/invitations'); }
        catch (error) { this.error = error.message; }
      },
      async respondInvitation(invitation, accept) {
        this.loading = true; this.error = '';
        try {
          const result = await api(`/invitations/${encodeURIComponent(invitation.id)}/${accept ? 'accept' : 'decline'}`, 'POST', {});
          if (accept) location.assign(`/Room/Index/${encodeURIComponent(result.roomId)}`);
          else this.invitations = this.invitations.filter(item => item.id !== invitation.id);
        } catch (error) { this.error = error.message; await this.loadInvitations(); } finally { this.loading = false; }
      },
      async connectRoomHub() {
        if (!window.signalR) { this.realtimeStatus = 'Realtime không khả dụng'; return; }
        const hub = new signalR.HubConnectionBuilder().withUrl('/hubs/rooms', { accessTokenFactory: ensureSignalRToken }).withAutomaticReconnect().build();
        this.roomHub = hub;
        hub.on('RoomStateSynced', state => { if (state.id === this.roomId) this.room = state; });
        hub.on('RoomChanged', async event => { if (event.roomId === this.roomId) await this.syncRoomFromApi(); });
        hub.on('RoomClosed', () => { this.notice = 'Phòng đã đóng.'; location.assign('/Lobby'); });
        hub.on('GameStarted', async event => { if (event.roomId === this.roomId) await this.syncRoomFromApi(true); });
        hub.on('InvitationReceived', async invitation => {
          if (page === 'lobby') { this.notice = `${invitation.inviterUsername} vừa mời bạn vào phòng ${invitation.roomName}.`; await this.loadInvitations(); }
        });
        hub.onreconnecting(() => { this.realtimeStatus = 'Mất kết nối · đang thử lại'; });
        hub.onreconnected(async () => {
          this.realtimeStatus = 'Đã kết nối';
          if (this.roomId) await hub.invoke('RequestRoomSync', this.roomId);
        });
        hub.onclose(() => { this.realtimeStatus = 'Mất kết nối'; if (!this.isUnmounted) this.scheduleRoomHubRetry(); });
        await this.startRoomHub();
      },
      async connectGameHub() {
        if (!window.signalR) { this.connectionStatus = 'Disconnected'; this.error = 'Realtime không khả dụng.'; return; }
        const hub = new signalR.HubConnectionBuilder().withUrl('/hubs/game', { accessTokenFactory: ensureSignalRToken })
          .withAutomaticReconnect([0, 1000, 3000, 5000, 10000]).build();
        this.gameHub = hub;
        hub.on('GameStateSynced', state => this.applyGameState(state));
        hub.on('ClockSynced', clock => this.applyClock(clock));
        hub.on('MoveApplied', event => this.applyMoveEvent(event));
        hub.on('MoveRejected', event => {
          if (event.gameId !== this.gameId) return;
          if (this.pendingMoveId === event.clientMoveId) this.pendingMoveId = null;
          this.error = event.message;
          if (event.errorCode === 'VERSION_CONFLICT') this.requestGameSync();
        });
        hub.on('PlayerConnectionChanged', event => {
          if (event.gameId === this.gameId && event.playerId !== this.user?.id) this.opponentConnection = event.connectionStatus;
        });
        hub.on('GameFinished', event => this.applyFinishedEvent(event));
        hub.on('ChatMessageReceived', event => this.mergeChat(event.message));
        hub.on('ChatMessageRejected', event => { if (event.gameId === this.gameId) this.chatError = event.message; });
        hub.on('IncomingCall', event => { this.incomingCall = event; this.callError = ''; });
        hub.on('CallStateChanged', event => this.onCallState(event));
        hub.on('CallSignalReceived', event => this.onCallSignal(event));
        hub.onreconnecting(() => { this.connectionStatus = 'Reconnecting'; this.clearLegalMoves(); });
        hub.onreconnected(async () => {
          this.connectionStatus = 'Connected';
          await this.requestGameSync();
          await this.joinInteractions();
          if (this.pendingMoveRequest) {
            try { await hub.invoke('SubmitMove', this.pendingMoveRequest); await this.requestGameSync(); }
            catch { this.error = 'Chưa xác nhận được nước đi. Đang giữ mã yêu cầu để thử lại an toàn.'; }
          }
        });
        hub.onclose(() => {
          this.connectionStatus = 'Disconnected';
          if (!this.isUnmounted) this.scheduleGameHubRetry();
        });
        await this.startGameHub();
      },
      async startGameHub() {
        try {
          if (this.gameHub?.state === signalR.HubConnectionState.Disconnected) {
            this.connectionStatus = 'Connecting';
            await this.gameHub.start();
            this.connectionStatus = 'Connected';
            await this.gameHub.invoke('JoinGame', this.gameId);
            await this.joinInteractions();
          }
        } catch { this.connectionStatus = 'Disconnected'; this.scheduleGameHubRetry(); }
      },
      scheduleGameHubRetry() {
        if (this.isUnmounted || this.gameRetryTimer || this.gameRetryAttempts >= 12) return;
        this.gameRetryTimer = setTimeout(async () => {
          this.gameRetryTimer = null; this.gameRetryAttempts++;
          await this.startGameHub();
        }, Math.min(15000, 1000 * (this.gameRetryAttempts + 1)));
      },
      async requestGameSync() {
        try { if (this.gameHub?.state === signalR.HubConnectionState.Connected) await this.gameHub.invoke('RequestGameSync', this.gameId); }
        catch { this.connectionStatus = 'Reconnecting'; }
      },
      async joinInteractions() {
        if (this.gameHub?.state !== signalR.HubConnectionState.Connected) return;
        await this.gameHub.invoke('JoinInteraction', this.gameId);
        await this.loadChat();
      },
      mergeChat(message) {
        if (!message || message.gameId !== this.gameId || this.chatMessages.some(item => item.id === message.id)) return;
        this.chatMessages.push(message);
        this.chatMessages.sort((a, b) => Date.parse(a.createdAtUtc) - Date.parse(b.createdAtUtc));
        this.$nextTick(() => { if (this.$refs.chatList) this.$refs.chatList.scrollTop = this.$refs.chatList.scrollHeight; });
      },
      async loadChat(before = null) {
        try {
          const query = new URLSearchParams({ limit: '40' }); if (before) query.set('before', before);
          const page = await api(`/games/${encodeURIComponent(this.gameId)}/messages?${query}`);
          const existing = new Set(this.chatMessages.map(item => item.id));
          const fresh = page.items.filter(item => !existing.has(item.id));
          this.chatMessages = [...fresh, ...this.chatMessages].sort((a, b) => Date.parse(a.createdAtUtc) - Date.parse(b.createdAtUtc));
          this.chatHasMore = page.hasMore; if (page.nextCursor) this.chatCursor = page.nextCursor;
          if (!before) this.$nextTick(() => { if (this.$refs.chatList) this.$refs.chatList.scrollTop = this.$refs.chatList.scrollHeight; });
        } catch (error) { this.chatError = error.message; }
      },
      async loadOlderChat() { if (this.chatCursor) await this.loadChat(this.chatCursor); },
      async sendChatMessage() {
        const content = this.chatDraft.trim(); if (!content || !this.gameHub) return;
        const request = this.pendingChatRequest?.content === content ? this.pendingChatRequest : { gameId: this.gameId, clientMessageId: crypto.randomUUID(), content };
        this.pendingChatRequest = request;
        this.chatDraft = ''; this.chatError = '';
        try { const result = await this.gameHub.invoke('SendChatMessage', request); if (result.message) this.mergeChat(result.message); else if (!result.accepted) this.chatError = 'Tin nhắn chưa gửi được.'; if (result.accepted) this.pendingChatRequest = null; }
        catch { this.chatDraft = content; this.chatError = 'Mất kết nối. Tin nhắn vẫn nằm trong ô nhập để bạn thử lại.'; }
      },
      async startVideoCall() {
        this.callError = ''; this.callNotice = '';
        try {
          await this.acquireLocalMedia();
          const result = await this.gameHub.invoke('StartCall', this.gameId);
          if (!result.accepted) throw new Error(this.callErrorFor(result.errorCode));
          this.activeCall = result.call;
        } catch (error) { this.callError = error.message || 'Không thể bắt đầu cuộc gọi.'; this.cleanupMedia(); }
      },
      async acceptVideoCall() {
        try {
          await this.acquireLocalMedia();
          const result = await this.gameHub.invoke('AcceptCall', this.incomingCall.call.id);
          if (!result.accepted) throw new Error(this.callErrorFor(result.errorCode));
          this.activeCall = result.call; this.incomingCall = null; this.ensurePeerConnection();
        } catch (error) { this.callError = error.message || 'Không thể nhận cuộc gọi.'; this.cleanupMedia(); }
      },
      async rejectVideoCall() { try { await this.gameHub.invoke('RejectCall', this.incomingCall.call.id); } catch {} this.incomingCall = null; },
      async endVideoCall() { try { if (this.activeCall) await this.gameHub.invoke('EndCall', this.activeCall.id); } catch {} this.cleanupCall(); },
      async acquireLocalMedia() {
        if (!navigator.mediaDevices?.getUserMedia) throw new Error('Trình duyệt không hỗ trợ camera/micro hoặc trang chưa dùng HTTPS.');
        if (!this.iceServers) this.iceServers = await api('/rtc/ice-servers');
        try { this.localStream = await navigator.mediaDevices.getUserMedia({ audio: true, video: true }); }
        catch (error) { throw new Error(error.name === 'NotAllowedError' ? 'Bạn đã từ chối quyền camera/micro.' : error.name === 'NotFoundError' ? 'Không tìm thấy camera hoặc micro.' : 'Không thể mở camera/micro.'); }
        this.callMicEnabled = true; this.callCameraEnabled = true;
        this.$nextTick(() => { if (this.$refs.localVideo) this.$refs.localVideo.srcObject = this.localStream; });
      },
      ensurePeerConnection() {
        if (this.peerConnection) return this.peerConnection;
        const pc = new RTCPeerConnection({ iceServers: this.iceServers || [] });
        this.peerConnection = pc; this.remoteStream = new MediaStream();
        for (const track of this.localStream?.getTracks() || []) pc.addTrack(track, this.localStream);
        pc.ontrack = event => { for (const track of event.streams[0]?.getTracks() || [event.track]) this.remoteStream.addTrack(track); this.$nextTick(() => { if (this.$refs.remoteVideo) this.$refs.remoteVideo.srcObject = this.remoteStream; }); };
        pc.onicecandidate = event => { if (event.candidate && this.activeCall) this.gameHub.invoke('SendCallSignal', { callId: this.activeCall.id, gameId: this.gameId, payload: JSON.stringify(event.candidate) }, 'IceCandidate').catch(() => { this.callError = 'Mất kết nối khi gửi thông tin kết nối.'; }); };
        pc.onconnectionstatechange = () => { if (pc.connectionState === 'connected' && this.activeCall) this.gameHub.invoke('CallConnected', this.activeCall.id).catch(() => {}); if (['failed', 'closed'].includes(pc.connectionState) && pc.connectionState === 'failed') this.callError = 'Không thiết lập được đường truyền media.'; };
        return pc;
      },
      async sendOffer() {
        if (!this.activeCall || this.offerSent) return;
        const pc = this.ensurePeerConnection(); const offer = await pc.createOffer(); await pc.setLocalDescription(offer);
        await this.gameHub.invoke('SendCallSignal', { callId: this.activeCall.id, gameId: this.gameId, payload: JSON.stringify(pc.localDescription) }, 'Offer'); this.offerSent = true;
      },
      async onCallSignal(event) {
        try {
          if (!this.activeCall || event.callId !== this.activeCall.id) return;
          const pc = this.ensurePeerConnection(); const payload = JSON.parse(event.payload);
          if (event.kind === 'Offer') { await pc.setRemoteDescription(payload); for (const candidate of this.pendingIceCandidates.splice(0)) await pc.addIceCandidate(candidate); const answer = await pc.createAnswer(); await pc.setLocalDescription(answer); await this.gameHub.invoke('SendCallSignal', { callId: event.callId, gameId: this.gameId, payload: JSON.stringify(pc.localDescription) }, 'Answer'); }
          else if (event.kind === 'Answer') { await pc.setRemoteDescription(payload); for (const candidate of this.pendingIceCandidates.splice(0)) await pc.addIceCandidate(candidate); }
          else if (event.kind === 'IceCandidate') { if (pc.remoteDescription) await pc.addIceCandidate(payload); else this.pendingIceCandidates.push(payload); }
        } catch (error) { this.callError = 'Tín hiệu cuộc gọi không hợp lệ hoặc đã hết hạn.'; }
      },
      async onCallState(event) {
        const call = event?.call; if (!call || call.gameId !== this.gameId) return;
        this.activeCall = call; this.callNotice = event.message || '';
        if (call.status === 'Accepted' && call.callerUserId === this.user?.id) { try { await this.sendOffer(); } catch { this.callError = 'Không gửi được lời mời kết nối video.'; } }
        if (['Rejected', 'Missed', 'Ended', 'Failed'].includes(call.status)) { this.incomingCall = null; this.cleanupCall(); this.callNotice = event.message || ({ Rejected: 'Đối thủ đã từ chối cuộc gọi.', Missed: 'Cuộc gọi đã hết thời gian chờ.', Ended: 'Cuộc gọi đã kết thúc.', Failed: 'Cuộc gọi không thể kết nối.' })[call.status]; }
      },
      toggleCallMic() { this.callMicEnabled = !this.callMicEnabled; for (const track of this.localStream?.getAudioTracks() || []) track.enabled = this.callMicEnabled; },
      toggleCallCamera() { this.callCameraEnabled = !this.callCameraEnabled; for (const track of this.localStream?.getVideoTracks() || []) track.enabled = this.callCameraEnabled; },
      cleanupMedia() { this.peerConnection?.close(); this.peerConnection = null; this.localStream?.getTracks().forEach(track => track.stop()); this.localStream = null; this.remoteStream = null; this.pendingIceCandidates = []; this.offerSent = false; },
      cleanupCall() { this.cleanupMedia(); this.activeCall = null; this.incomingCall = null; },
      callErrorFor(code) { return ({ CALL_BUSY: 'Đang có cuộc gọi khác trong ván.', CALL_UNAVAILABLE: 'Đối thủ đang ngoại tuyến.', CALL_FORBIDDEN: 'Bạn không có quyền thực hiện cuộc gọi này.', CALL_STATE_CONFLICT: 'Trạng thái cuộc gọi đã thay đổi.' })[code] || 'Không thể thực hiện cuộc gọi.'; },
      applyGameState(state) {
        if (state.gameId !== this.gameId) return;
        if (this.game && state.version < this.game.version) return;
        this.serverClockOffsetMs = Date.parse(state.serverTimeUtc) - Date.now();
        this.clockState = { ...state.clock, activeSide: state.currentTurn, serverTimeUtc: state.serverTimeUtc };
        this.redPlayerName = state.redPlayerName || 'Đỏ'; this.blackPlayerName = state.blackPlayerName || 'Đen';
        this.moveHistory = state.moveHistory || [];
        this.game = { ...this.game, id: state.gameId, mode: 'Online', status: state.status, currentFen: state.fen,
          currentTurn: state.currentTurn, version: state.version, ply: this.moveHistory.length, isInCheck: state.isInCheck, winnerId: state.winnerId,
          resultReason: state.resultReason, endedAtUtc: state.endedAtUtc, redRemainingMs: state.clock.redRemainingMs,
          blackRemainingMs: state.clock.blackRemainingMs, turnStartedAtUtc: state.clock.turnStartedAtUtc };
        if (this.pendingMoveRequest && state.version > this.pendingMoveRequest.expectedGameVersion) {
          this.pendingMoveId = null; this.pendingMoveRequest = null;
        }
        this.clearLegalMoves();
      },
      applyClock(clock) {
        if (clock.gameId && clock.gameId !== this.gameId) return;
        if (this.game && clock.version !== undefined && clock.version < this.game.version) return;
        this.serverClockOffsetMs = Date.parse(clock.serverTimeUtc) - Date.now();
        this.clockState = { redRemainingMs: clock.redRemainingMs, blackRemainingMs: clock.blackRemainingMs,
          activeSide: clock.activeSide, turnStartedAtUtc: clock.turnStartedAtUtc, serverTimeUtc: clock.serverTimeUtc };
      },
      applyMoveEvent(event) {
        if (event.gameId !== this.gameId) return;
        if (this.game && event.version <= this.game.version) { if (this.pendingMoveId === event.clientMoveId) { this.pendingMoveId = null; this.pendingMoveRequest = null; } return; }
        if (this.game && event.version !== this.game.version + 1) { this.requestGameSync(); return; }
        this.serverClockOffsetMs = Date.parse(event.serverTimeUtc) - Date.now();
        this.clockState = { ...event.clock, serverTimeUtc: event.serverTimeUtc };
        this.game = { ...this.game, currentFen: event.fenAfter, currentTurn: event.nextTurn, version: event.version,
          ply: event.ply, status: event.gameStatus, isInCheck: event.isInCheck, winnerId: event.winnerId, resultReason: event.resultReason,
          redRemainingMs: event.clock.redRemainingMs, blackRemainingMs: event.clock.blackRemainingMs };
        const fromFile = event.fromSquare.charCodeAt(0) - 97; const fromRank = Number(event.fromSquare[1]);
        const toFile = event.toSquare.charCodeAt(0) - 97; const toRank = Number(event.toSquare[1]);
        const piece = this.game.currentFen ? piecesFromFen(this.moveHistory.length ? this.game.currentFen : '') : [];
        this.moveHistory = [...this.moveHistory, { ply: event.ply, fromSquare: event.fromSquare, toSquare: event.toSquare,
          piece: piece.find(item => item.file === toFile && item.rank === toRank)?.name || '', capturedPiece: event.capturedPiece,
          createdAt: event.serverTimeUtc, fromFile, fromRank, toFile, toRank }];
        if (this.pendingMoveId === event.clientMoveId) { this.pendingMoveId = null; this.pendingMoveRequest = null; }
        this.error = ''; this.clearLegalMoves();
        if (event.gameStatus === 'Finished') this.requestGameSync();
      },
      applyFinishedEvent(event) {
        if (event.gameId !== this.gameId || !this.game || event.finalVersion < this.game.version) return;
        this.serverClockOffsetMs = Date.parse(event.serverTimeUtc) - Date.now();
        this.game = { ...this.game, currentFen: event.finalFen, status: 'Finished', version: event.finalVersion,
          winnerId: event.winnerId, resultReason: event.resultReason, endedAtUtc: event.endedAtUtc,
          redRemainingMs: event.clock.redRemainingMs, blackRemainingMs: event.clock.blackRemainingMs };
        this.pendingMoveId = null; this.pendingMoveRequest = null; this.clearLegalMoves();
      },
      clockRemaining(side) {
        if (!this.game) return 0;
        const state = this.clockState || this.game;
        let remaining = side === 'Red' ? state.redRemainingMs : state.blackRemainingMs;
        if (this.game.mode === 'Online' && this.game.status === 'InProgress' && state.activeSide === side && state.serverTimeUtc) {
          const now = this.clockNow + this.serverClockOffsetMs;
          remaining -= Math.max(0, now - Date.parse(state.serverTimeUtc));
        }
        return Math.max(0, remaining);
      },
      formatClock(milliseconds) {
        const total = Math.ceil(milliseconds / 1000); return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`;
      },
      async startRoomHub() {
        try {
          await this.roomHub.start();
          this.realtimeStatus = 'Đã kết nối';
          if (this.roomId) await this.roomHub.invoke('JoinRoomGroup', this.roomId);
        } catch {
          this.realtimeStatus = 'Đang thử kết nối lại…';
          this.scheduleRoomHubRetry();
        }
      },
      async syncRoomFromApi(openGame = false) {
        try {
          this.room = await api(`/rooms/${encodeURIComponent(this.roomId)}`);
          if (openGame && this.room.gameId) location.assign(`/Game/Index/${encodeURIComponent(this.room.gameId)}`);
        } catch (error) { if (error.status !== 404) this.error = error.message; }
      },
      scheduleRoomHubRetry() {
        if (this.isUnmounted || this.roomRetryTimer) return;
        this.roomRetryTimer = setTimeout(async () => { this.roomRetryTimer = null; if (this.roomHub?.state === signalR.HubConnectionState.Disconnected) await this.startRoomHub(); }, 5000);
      },
      async changeSide(side) {
        await this.roomMutation('side', { side, expectedVersion: this.room.version });
      },
      async toggleReady() {
        await this.roomMutation('ready', { isReady: !this.myMembership?.isReady, expectedVersion: this.room.version });
      },
      async roomMutation(action, payload) {
        this.loading = true; this.error = '';
        try { this.room = await api(`/rooms/${encodeURIComponent(this.roomId)}/${action}`, 'POST', payload); }
        catch (error) { this.error = error.message; this.room = await api(`/rooms/${encodeURIComponent(this.roomId)}`).catch(() => this.room); }
        finally { this.loading = false; }
      },
      async startRoom() { await this.roomMutation('start', { expectedVersion: this.room.version }); },
      async leaveRoom() {
        this.loading = true; this.error = '';
        try { await api(`/rooms/${encodeURIComponent(this.roomId)}/leave`, 'POST', {}); await this.roomHub?.invoke('LeaveRoomGroup', this.roomId); location.assign('/Lobby'); }
        catch (error) { this.error = error.message; } finally { this.loading = false; }
      },
      async sendInvitation() {
        this.loading = true; this.error = ''; this.notice = '';
        try { await api(`/rooms/${encodeURIComponent(this.roomId)}/invitations`, 'POST', { username: this.inviteUsername }); this.notice = `Đã gửi lời mời đến ${this.inviteUsername}.`; this.inviteUsername = ''; this.showInviteModal = false; }
        catch (error) { this.error = error.message; } finally { this.loading = false; }
      },
      async copyCode() {
        try { await navigator.clipboard.writeText(this.room.code); this.notice = 'Đã sao chép mã phòng.'; }
        catch { this.notice = `Mã phòng: ${this.room.code}`; }
      },
      clearLegalMoves() { this.legalTargets = []; },
      showLocalTurnMessage() {
        const side = this.game?.currentTurn === 'Red' ? 'Đỏ' : 'Đen';
        this.error = `Hiện là lượt ${side}. Hãy chọn quân ${side}.`;
      },
      async loadLegalMoves(position) {
        this.error = ''; this.legalTargets = [];
        try { this.legalTargets = await api(`/games/${encodeURIComponent(this.gameId)}/legal-moves?fromFile=${position.file}&fromRank=${position.rank}`); }
        catch (error) { this.error = error.message; }
      },
      async applyMove(move) {
        this.loading = true; this.error = ''; this.legalTargets = [];
        try {
          const moveData = {
            fromFile: move.from.file, fromRank: move.from.rank, toFile: move.to.file, toRank: move.to.rank,
            expectedVersion: this.game.version, clientMoveId: crypto.randomUUID(),
          };
          if (this.game.mode === 'Local') {
            this.game = await api('/games/local/move', 'POST', { gameId: this.game.id, ...moveData });
            this.moveHistory = await api(`/games/${encodeURIComponent(this.gameId)}/moves`);
          } else {
            if (this.gameHub?.state !== signalR.HubConnectionState.Connected) throw new Error('Đang mất kết nối tới ván đấu.');
            const request = { gameId: this.game.id, clientMoveId: moveData.clientMoveId,
              fromSquare: `${String.fromCharCode(97 + moveData.fromFile)}${moveData.fromRank}`,
              toSquare: `${String.fromCharCode(97 + moveData.toFile)}${moveData.toRank}`, expectedGameVersion: moveData.expectedVersion };
            this.pendingMoveId = request.clientMoveId; this.pendingMoveRequest = request;
            const result = await this.gameHub.invoke('SubmitMove', request);
            if (!result.accepted) { this.pendingMoveId = null; this.pendingMoveRequest = null; await this.requestGameSync(); }
          }
        } catch (error) { this.error = error.message; if (this.game?.mode === 'Online') await this.requestGameSync(); }
        finally { this.loading = false; }
      },
      async confirmResign() {
        this.loading = true; this.error = '';
        try { await this.gameHub.invoke('ResignGame', this.gameId); this.showResignDialog = false; await this.requestGameSync(); }
        catch (error) { this.error = error.message; }
        finally { this.loading = false; }
      },
      async restartLocalGame() {
        this.loading = true; this.error = '';
        try {
          const game = await api(`/games/${encodeURIComponent(this.gameId)}/restart`, 'POST', { expectedVersion: this.game.version });
          location.assign(`/Game/Index/${encodeURIComponent(game.id)}`);
        } catch (error) { this.error = error.message; } finally { this.loading = false; }
      },
      async logout() { try { await api('/auth/logout', 'POST', {}); } finally { accessToken = null; location.assign('/'); } },
      async logoutAll() { try { await api('/auth/logout-all', 'POST', {}); } finally { accessToken = null; location.assign('/'); } },
    },
  }).mount(root);
}
