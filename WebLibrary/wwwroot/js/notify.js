function showNotifyModal(msg) {
    let modal = document.getElementById('notifyModal');
    if (!modal) {
        modal = document.createElement('div');
        modal.className = 'modal fade show';
        modal.id = 'notifyModal';
        modal.tabIndex = -1;
        modal.style.display = 'block';
        modal.style.background = 'rgba(0,0,0,0.5)';
        modal.innerHTML = `
        <div class='modal-dialog'>
            <div class='modal-content'>
                <div class='modal-header'>
                    <h5 class='modal-title'>借阅通知</h5>
                    <button type='button' class='btn-close' onclick="document.getElementById('notifyModal').remove();"></button>
                </div>
                <div class='modal-body'><p>${msg}</p></div>
                <div class='modal-footer'>
                    <button type='button' class='btn btn-primary' onclick="document.getElementById('notifyModal').remove();">确定</button>
                </div>
            </div>
        </div>`;
        document.body.appendChild(modal);
    }
}
setInterval(function() {
    fetch('/api/CheckReservationNotify').then(res => res.json()).then(data => {
        if (data && data.message) {
            showNotifyModal(data.message);
        }
    });
}, 10000); 