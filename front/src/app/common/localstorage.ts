export class LocalStorageData {
    
    public obterUsuario() {
        const user = localStorage.getItem('myapp.user');
        return user ? JSON.parse(user) : null;
    }

    public salvarDadosLocaisUsuario(response: any) {
        this.salvarTokenUsuario(response.accessToken);
        this.salvarUsuario(response.userToken);
    }

    public limparDadosLocaisUsuario() {
        localStorage.removeItem('myapp.token');
        localStorage.removeItem('myapp.user');
    }

    public obterTokenUsuario(): string | null {
        return localStorage.getItem('myapp.token');
    }

    public salvarTokenUsuario(token: string) {
        localStorage.setItem('myapp.token', token);
    }

    public salvarUsuario(user: string) {
        localStorage.setItem('myapp.user', JSON.stringify(user));
    }

}