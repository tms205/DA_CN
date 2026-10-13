let taiGoogle;
const cacNut = new WeakMap();

function napThuVien() {
    if (window.google?.accounts?.id) return Promise.resolve();
    if (taiGoogle) return taiGoogle;
    taiGoogle = new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = 'https://accounts.google.com/gsi/client';
        script.async = true;
        const timer = setTimeout(() => {
            script.remove();
            taiGoogle = null;
            reject(new Error('Google load timeout.'));
        }, 20000);
        script.onload = () => {
            clearTimeout(timer);
            if (window.google?.accounts?.id) resolve();
            else { taiGoogle = null; reject(new Error('Google unavailable.')); }
        };
        script.onerror = () => {
            clearTimeout(timer); script.remove(); taiGoogle = null;
            reject(new Error('Google load failed.'));
        };
        document.head.appendChild(script);
    });
    return taiGoogle;
}

export async function hienNut(element, clientId, dotnet) {
    const state = { daDong: false };
    cacNut.set(element, state);
    await napThuVien();
    if (state.daDong || !element.isConnected) return;
    window.google.accounts.id.initialize({
        client_id: clientId, auto_select: false, ux_mode: 'popup',
        callback: response => {
            if (!state.daDong && element.isConnected && response.credential)
                dotnet.invokeMethodAsync('NhanCredential', response.credential).catch(() => {});
        }
    });
    window.google.accounts.id.renderButton(element, {
        type: 'standard', theme: 'outline', size: 'large', text: 'continue_with',
        shape: 'rectangular', locale: 'vi', width: Math.min(400, Math.max(200, element.clientWidth))
    });
}

export function dongNut(element) {
    const state = cacNut.get(element);
    if (state) state.daDong = true;
    cacNut.delete(element);
}
