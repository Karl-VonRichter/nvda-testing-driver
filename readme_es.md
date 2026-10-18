# NVDA Testing Driver

[Read in English](readme.md)

Automatiza pruebas de accesibilidad con el lector de pantalla NVDA en Windows. El controlador inicia NVDA, le envía comandos de teclado reales y te devuelve el texto que NVDA pronuncia, para que tus pruebas comprueben los nombres, roles, estados y contenidos que oiría una persona que usa un lector de pantalla.

Sirve para sitios web (con Selenium) y para aplicaciones de escritorio.

## Compatibilidad

| | Compatible |
| --- | --- |
| NVDA | 2025.1 o posterior, instalado o en una ruta indicada (recomendado). Si no hay ninguno, se usa la copia portable incluida de NVDA 2018.4.1. |
| Navegadores | Chrome, Edge y Chromium mediante Selenium. Firefox no se ha probado: la gestión de ventanas no depende del navegador, pero los ejemplos no lo inician. Internet Explorer no es compatible. |
| Tus proyectos de prueba | Cualquiera que pueda usar .NET Standard 2.0, como .NET 8 o posterior y .NET Framework 4.7.2 o posterior. Se prueban .NET 10 y .NET Framework 4.8. |
| Dependencias | Selenium.WebDriver 4.x y MSTest 3.x (para `NvdaTestingDriver.MSTest`). |
| Sistema operativo | Windows, con un escritorio interactivo desbloqueado. No se pueden usar navegadores sin interfaz (headless) ni agentes de compilación no interactivos. |
| Última verificación | NVDA 2026.2 y Chrome 154 en Windows 11. |

¿Vienes de la versión 0.2.0-beta? Consulta los cambios incompatibles en [CHANGELOG.md](CHANGELOG.md) (en inglés).

## Paquetes

- `NvdaTestingDriver` inicia NVDA, envía comandos y captura lo que dice.
- `NvdaTestingDriver.Selenium` encuentra la ventana del navegador de una sesión de WebDriver, la trae al primer plano y mueve el foco a elementos de la página.
- `NvdaTestingDriver.MSTest` añade `NvdaAssert.TextContains`, que ignora diferencias de mayúsculas, espacios y guiones pronunciados.

Los paquetes publicados en NuGet son la versión anterior, 0.2.0-beta. Hasta que se publique una nueva, referencia directamente los proyectos de `src/`.

## Tu primera prueba

Esta prueba abre la página de componentes de la aplicación de ejemplo en Chrome, enfoca el campo de correo y comprueba lo que NVDA dice de él. Inicia antes la aplicación de ejemplo (consulta [Ejecutar los ejemplos](#ejecutar-los-ejemplos)).

```csharp
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver;
using NvdaTestingDriver.Commands.NvdaCommands;
using NvdaTestingDriver.MSTest;
using NvdaTestingDriver.Selenium;
using NvdaTestingDriver.Selenium.Extensions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

[TestClass]
[DoNotParallelize]
public class ComponentsPageShould
{
    private static readonly WebDriverWrapper Wrapper = new WebDriverWrapper();
    private static IWebDriver _browser;
    private static NvdaDriver _nvda;

    [ClassInitialize]
    public static async Task Start(TestContext context)
    {
        var options = new ChromeOptions();
        // Crea el árbol de accesibilidad de Chrome aunque Chrome se inicie antes que NVDA.
        options.AddArgument("--force-renderer-accessibility");
        _browser = Wrapper.UpWebDriver(() => new ChromeDriver(options));

        _nvda = new NvdaDriver(nvda => nvda.BrowseModeSettings.AutoSayAllOnPageLoad = false);
        // Las teclas enviadas a través de NVDA son pulsaciones reales: envíalas solo al navegador de pruebas.
        _nvda.BeforeSendingKeys = Wrapper.EnsureBrowserWindowForeground;
        await _nvda.ConnectAsync();
    }

    [ClassCleanup]
    public static async Task Stop()
    {
        await _nvda.DisconnectAsync();
        _browser.Quit();
    }

    [TestMethod]
    public async Task AnnounceTheEmailField()
    {
        _browser.Navigate().GoToUrl("http://127.0.0.1:5000/home/components");
        _browser.Focus(_browser.FindElement(By.Id("example-email")));

        string speech = await _nvda.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportCurrentFocus);

        NvdaAssert.TextContains(speech, "Email");
        NvdaAssert.TextContains(speech, "edit");
        NvdaAssert.TextContains(speech, "required");
    }
}
```

Los textos esperados están en inglés porque la aplicación de ejemplo y NVDA están en inglés. Con NVDA en otro idioma, usa lo que diga tu NVDA.

`src/samples/TestSupport/SampleTestSession.cs` es una versión más completa de esta configuración: admite Edge y Chromium, oculta los avisos de automatización de Chrome y devuelve el foco a la página cuando acaba en la barra de herramientas del navegador.

## Averiguar lo que dice NVDA

Lo más fácil es escribir las pruebas a partir de lo que NVDA dice realmente. El visualizador de voz de NVDA muestra cada anuncio como texto:

1. Inicia NVDA. Abre el menú de NVDA (NVDA+N) y elige Herramientas, Visualizador de voz.
2. Abre la página que quieres probar y ve al elemento como lo haría una persona usuaria: con Tab o con las teclas del modo exploración, como H para el siguiente encabezado y F para el siguiente campo de formulario. NVDA+Tab anuncia el elemento que tiene el foco.
3. Copia las líneas del visualizador de voz.
4. En la prueba, llega al mismo elemento (con `Focus` o con el mismo comando de NVDA) y comprueba lo importante: el nombre, el rol y los estados relevantes. Evita comprobar la línea completa, porque el texto cambia entre versiones de NVDA, navegadores e idiomas.
5. Cierra NVDA (NVDA+Q) antes de ejecutar las pruebas.

Al ejecutar una prueba, el controlador cierra cualquier NVDA en ejecución e inicia el suyo, y no vuelve a abrir el tuyo al terminar. Si usas otro lector de pantalla, desactívalo durante la ejecución: hablará a la vez que la prueba y puede mover el foco.

## Elegir NVDA

Por defecto, el controlador usa un NVDA 2025.1 o posterior instalado y se conecta mediante su Acceso remoto integrado. Para cambiarlo:

- `NvdaExecutablePath` elige un ejecutable concreto de NVDA 2025.1 o posterior, instalado o portable.
- `UseInstalledNvda = false` usa la copia incluida de NVDA 2018.4.1 con su complemento Remote. Se mantiene por compatibilidad, pero lee mal las páginas modernas, así que usa un NVDA actual para probar sitios web.

```csharp
var nvda = new NvdaDriver(options =>
{
    options.NvdaExecutablePath = @"C:\Tools\nvda\nvda.exe";
});
```

Cada conexión tiene su propia carpeta de configuración de NVDA en `%TEMP%\NvdaTestingDriver` y una clave aleatoria de Acceso remoto. Tu configuración de NVDA no se modifica nunca. El controlador se conecta a NVDA en `127.0.0.1:6837` sin usar ningún servidor externo. El perfil de pruebas registra a nivel INFO (no DEBUG, que guardaría todo lo pronunciado) y desactiva el seguimiento del ratón. La carpeta se elimina al desconectar; un cierre inesperado o un archivo bloqueado puede dejarla en disco.

NVDA solo ejecuta una instancia a la vez, así que ejecuta las pruebas con lector de pantalla de una en una y en un escritorio que no estés usando. `NvdaDriver.NvdaVersion` indica la versión del NVDA iniciado.

## Enviar comandos y comprobar lo que se dice

Los comandos habituales de NVDA están en `NvdaTestingDriver.Commands.NvdaCommands`, agrupados como en la [guía del usuario de NVDA](https://www.nvaccess.org/files/nvda/documentation/userGuide.html). Se admiten las distribuciones de teclado de escritorio y de portátil.

- `SendCommandAndGetSpokenTextAsync` y `SendKeysAndGetSpokenTextAsync` envían la entrada y devuelven lo que dice NVDA cuando se queda en silencio un momento.
- `SendCommandAsync` y `SendKeysAsync` envían la entrada sin esperar a que hable.
- `GetNextSpokenMessageAsync` empieza a escuchar, ejecuta la acción que le pases y devuelve lo que se dice a continuación.
- `StopReadingAsync` hace que NVDA deje de hablar.

Usa `NvdaAssert.TextContains` (o `NvdaTestHelper.TextContains`, que devuelve un booleano) para comparar lo pronunciado.

En sitios web, asigna `WebDriverWrapper.EnsureBrowserWindowForeground` a `BeforeSendingKeys`, como en el ejemplo. Las teclas van a la ventana que esté en primer plano, así que la comprobación se niega a enviarlas si no puede traer el navegador de pruebas al frente. `webDriver.Focus(element)` enfoca un elemento y solo añade `tabindex="-1"` cuando no puede recibir el foco de otro modo.

## Ejecutar los ejemplos

`src/samples/AccessibleDemo` es un pequeño sitio local con una página de contacto, un árbol de WCAG y una página de componentes con un campo obligatorio, una alerta de validación, un diálogo y una tabla. Para explorarlo:

```powershell
dotnet run --project src/samples/AccessibleDemo --no-launch-profile --urls http://127.0.0.1:5000
```

`AccessibleDemo.Core.Tests` inicia la aplicación compilada en un puerto local libre, ejecuta pruebas con NVDA y la cierra al terminar. Las pruebas con lector de pantalla solo se ejecutan cuando lo pides:

```powershell
$env:NVDA_TEST_RUN_INTERACTIVE = 'true'
dotnet test src/samples/AccessibleDemo.Core.Tests/AccessibleDemo.Core.Tests.csproj --configuration Release
Remove-Item Env:NVDA_TEST_RUN_INTERACTIVE
```

| Variable | Significado |
| --- | --- |
| `NVDA_TEST_RUN_INTERACTIVE` | `true` para ejecutar los ejemplos con lector de pantalla. Si no, se omiten. |
| `NVDA_TEST_BROWSER` | `chrome` (por defecto), `edge` o `chromium` |
| `NVDA_TEST_BROWSER_BINARY` | Ruta del ejecutable del navegador, si no es la predeterminada |
| `NVDA_TEST_NVDA_EXECUTABLE` | Ruta de un ejecutable de NVDA 2025.1 o posterior |
| `NVDA_TEST_USE_INSTALLED` | `false` para usar la copia incluida de NVDA 2018.4.1 |

Selenium Manager localiza el controlador del navegador. Con `chromium` y sin ruta, descarga la versión estable de Chrome for Testing.

`RemoteWebsites.Tests` hace comprobaciones parecidas en una página que tú configures: el anuncio de un elemento, regiones con nombre y el cierre de un diálogo con Escape. No incluye ningún sitio predeterminado. Prueba únicamente sitios que estés autorizado a probar. La configuración, y los registros privados que escriben los ejemplos, se describen en [docs/evidence-and-reporting.md](docs/evidence-and-reporting.md) (en inglés).

## Compilar y probar este repositorio

Necesitas Windows y el SDK de .NET 10 o posterior.

```powershell
dotnet build src/NvdaTestingDriver.sln --configuration Release
dotnet test src/NvdaTestingDriver.sln --configuration Release --no-build
```

Así se ejecutan las pruebas de regresión del controlador y una prueba de compatibilidad con .NET Framework 4.8. No inician NVDA ni un navegador, así que funcionan en cualquier equipo, incluida la integración continua. Los ejemplos con lector de pantalla se omiten salvo que `NVDA_TEST_RUN_INTERACTIVE` sea `true`.

GitHub Actions compila y ejecuta estas pruebas en un agente Windows alojado para los envíos y solicitudes de cambios a `master`, `main` y `develop`. Un trabajo que se lanza manualmente ejecuta los ejemplos locales con lector de pantalla en un agente propio con la etiqueta `nvda-interactive`, que necesita un escritorio desbloqueado, un navegador y NVDA. Las solicitudes de cambios nunca se ejecutan en ese equipo.

## Licencia y agradecimientos

NVDA Testing Driver lo creó Juan José Montiel y se distribuye con la Licencia Pública General de GNU, versión 3; consulta [license.txt](license.txt).

Este proyecto no habría sido posible sin todas las personas que me han ayudado con algún aspecto del mismo:

- A [Pablo Núñez](https://www.twitter.com/pablonete), que en una conversación por Twitter me dio la idea de hacer tests con lector de pantalla, y fue la semilla de este proyecto. ¡Ojalá puedas usarlo en tus desarrollos! :)
- A [Christopher Toth](https://www.twitter.com/mongoose_q) y a Tyler Spivey, que desarrollaron el maravilloso complemento para NVDA [NvdaRemote](https://www.nvdaremote.com), y que me ha permitido controlar reomtamente NVDA.
- A [Jose Manuel Delicado](https://www.twitter.com/jmdaweb), que me ayudó a entender un poco mejor el complemento de NvdaRemote y a modificarlo para poder depurarlo.
- Y por último y no menos importante, a mi mujer, [Núria](https://www.twitter.com/amaterasu_n), quien aguanta mis horas sentado delante del ordenador, y se encarga de todo durante esas horas en las que estoy inmerso en este mundo de códigos. ¡Gracias, mi vida!
