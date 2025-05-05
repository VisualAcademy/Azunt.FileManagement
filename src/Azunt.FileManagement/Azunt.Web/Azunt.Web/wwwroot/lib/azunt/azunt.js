// Azunt 네임스페이스를 전역(window)에 정의 (이미 존재하면 덮어쓰지 않음)
window.Azunt = window.Azunt || {};

// Azunt.TimeZone 도메인을 만들어 함수 등록
window.Azunt.TimeZone = {
    // 브라우저의 로컬 시간대 오프셋을 분 단위로 반환
    // 예: -540 (한국, UTC+9)
    getLocalOffsetMinutes: function () {
        return new Date().getTimezoneOffset();
    }
};
