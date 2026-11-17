namespace UniHome.Api.Configuration;

/// <summary>
/// Índice de Swagger UI con un acceso visible al cierre de la sesión
/// documental. Los scripts siguen siendo externos y la CSP no habilita
/// JavaScript inline.
/// </summary>
internal static class SwaggerUiIndexHtml
{
    public const string Content =
        """
        <!-- HTML for static distribution bundle build -->
        <!DOCTYPE html>
        <html lang="es">
        <head>
            <meta charset="UTF-8">
            <meta name="robots" content="noindex, nofollow">
            <title>UniHome API · Documentación interna</title>
            <link rel="stylesheet" type="text/css" href="./swagger-ui.css">
            <link rel="stylesheet" type="text/css" href="./index.css">
            <link rel="icon" type="image/png" href="./favicon-32x32.png" sizes="32x32" />
            <link rel="icon" type="image/png" href="./favicon-16x16.png" sizes="16x16" />
        </head>
        <body>
            <div style="display:flex;align-items:center;justify-content:space-between;gap:16px;padding:10px 18px;background:#0f5132;color:#fff;font-family:Arial,Helvetica,sans-serif;font-size:14px;">
                <span><strong>UniHome API</strong> · Documentación interna</span>
                <a href="/swagger/logout" style="color:#fff;text-decoration:underline;">Cerrar sesión</a>
            </div>
            <div id="swagger-ui"></div>
            <script src="./swagger-ui-bundle.js" charset="utf-8"></script>
            <script src="./swagger-ui-standalone-preset.js" charset="utf-8"></script>
            <script src="./index.js" charset="utf-8"></script>
        </body>
        </html>
        """;
}
