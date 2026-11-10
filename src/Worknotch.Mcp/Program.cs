// worknotch-mcp: el servidor que conecta Claude con Worknotch. Lo arranca Claude, no quien usa la app:
//   claude mcp add worknotch -- "%LOCALAPPDATA%\Programs\Worknotch\worknotch-mcp.exe"
using System.Reflection;
using System.Text;
using Worknotch.Core;
using Worknotch.Core.Mcp;

var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
using var input = new StreamReader(Console.OpenStandardInput(), utf8);
using var output = new StreamWriter(Console.OpenStandardOutput(), utf8);
string version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";
await new McpServer(Store.Default(), new SystemClock(), version).RunAsync(input, output);
