using System.Net;

namespace UniHome.Api.Security;

/// <summary>
/// Formularios HTML mínimos para el acceso documental. No contienen JavaScript,
/// no reciben ni muestran JWT y solo publican a rutas de /swagger.
/// </summary>
internal static class SwaggerLoginPage
{
    public static string Login(
        string formFieldName,
        string requestToken,
        string? error = null,
        string? email = null,
        bool loggedOut = false)
    {
        var message = error ?? (loggedOut
            ? "La sesión documental se cerró correctamente."
            : null);

        return Page(
            title: "Acceso a documentación",
            body: $$"""
                <h1>Documentación UniHome</h1>
                <p class="muted">Solo cuentas con permiso de documentación.</p>
                {{(message is null ? string.Empty : $"<p class=\"message\" role=\"status\">{Encode(message)}</p>")}}
                <form method="post" action="/swagger/login" autocomplete="on">
                    <input type="hidden" name="{{Encode(formFieldName)}}" value="{{Encode(requestToken)}}">
                    <label for="correo">Correo electrónico</label>
                    <input id="correo" name="correo" type="email" autocomplete="username" required value="{{Encode(email)}}">
                    <label for="contrasena">Contraseña</label>
                    <input id="contrasena" name="contrasena" type="password" autocomplete="current-password" required>
                    <button type="submit">Iniciar sesión</button>
                </form>
                <p class="muted small">La sesión de documentación es breve (15 minutos por defecto) y no concede acceso a la API.</p>
                """);
    }

    public static string HttpsRequired() =>
        Page(
            title: "HTTPS requerido",
            body: """
                <h1>HTTPS requerido</h1>
                <p class="muted">La documentación solo inicia sesión mediante HTTPS. No se emitió ninguna cookie.</p>
                """);

    public static string Logout(string formFieldName, string requestToken) =>
        Page(
            title: "Cerrar sesión de documentación",
            body: $$"""
                <h1>Cerrar sesión</h1>
                <p class="muted">Se eliminará la cookie de documentación de este navegador.</p>
                <form method="post" action="/swagger/logout">
                    <input type="hidden" name="{{Encode(formFieldName)}}" value="{{Encode(requestToken)}}">
                    <button type="submit">Cerrar sesión</button>
                </form>
                <p><a href="/swagger">Volver a la documentación</a></p>
                """);

    private static string Page(string title, string body) =>
        $$"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex, nofollow">
            <title>{{Encode(title)}} · UniHome</title>
            <style>
                :root { color-scheme: light; }
                body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #f4f6f8; font-family: Arial, Helvetica, sans-serif; color: #1f2933; }
                main { width: min(92vw, 420px); background: #fff; border-radius: 12px; padding: 32px; box-shadow: 0 8px 30px rgba(15, 23, 42, .12); }
                h1 { margin: 0 0 8px; font-size: 24px; color: #0f5132; }
                label { display: block; margin: 18px 0 6px; font-weight: 700; }
                input { box-sizing: border-box; width: 100%; padding: 10px 12px; border: 1px solid #cbd2d9; border-radius: 8px; font-size: 16px; }
                button { width: 100%; margin-top: 22px; padding: 11px 16px; border: 0; border-radius: 8px; background: #0f5132; color: #fff; font-size: 16px; font-weight: 700; cursor: pointer; }
                .muted { color: #52606d; }
                .small { font-size: 13px; }
                .message { padding: 10px 12px; border-radius: 8px; background: #fff4e5; color: #7c2d12; }
            </style>
        </head>
        <body>
            <main>
                {{body}}
            </main>
        </body>
        </html>
        """;

    private static string Encode(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
