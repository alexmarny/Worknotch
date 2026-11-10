; Instalador de Windows de Worknotch (Inno Setup 6).
; Lo compila la CI después de publicar la app en dist\app:
;   iscc /DAppVersion=1.2.0 build\instalador.iss   ->   dist\Worknotch-setup.exe
; Se instala para el usuario actual, sin pedir permisos de administrador, en
; %LOCALAPPDATA%\Programs\Worknotch, con acceso en el menú Inicio y desinstalación desde
; Configuración > Aplicaciones. La propia app lo usa para actualizarse:
;   Worknotch-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /FORCECLOSEAPPLICATIONS /relanzar=1
; y, con /relanzar=1, vuelve a abrirse al terminar.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
; No cambiar AppId: es lo que identifica la instalación al actualizar y al desinstalar.
AppId={{7C0E2B4A-93D1-4F5E-A1B8-6D2F0C9E5A37}
AppName=Worknotch
AppVersion={#AppVersion}
AppVerName=Worknotch {#AppVersion}
AppPublisher=Alejandro
AppComments=Tu día, a un gesto: tareas, foco y notas en el borde de la pantalla
DefaultDirName={localappdata}\Programs\Worknotch
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=Worknotch-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=Worknotch
UninstallDisplayIcon={app}\Worknotch.exe
SetupIconFile=icono\icono.ico
CloseApplications=force
RestartApplications=no

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "arranque"; Description: "Abrir Worknotch al iniciar Windows"

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Worknotch"; Filename: "{app}\Worknotch.exe"

[Registry]
; Los enlaces worknotch:// (por ejemplo, worknotch://add?title=Comprar%20pan) abren la app
Root: HKCU; Subkey: "Software\Classes\worknotch"; ValueType: string; ValueData: "URL:Worknotch"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\worknotch"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\worknotch\shell\open\command"; ValueType: string; ValueData: """{app}\Worknotch.exe"" ""%1"""
; Arranque con Windows. Después se cambia desde Ajustes; al desinstalar se quita.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Worknotch"; ValueData: """{app}\Worknotch.exe"" --inicio"; Tasks: arranque; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Worknotch"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\Worknotch.exe"; Description: "Abrir Worknotch"; Flags: nowait postinstall skipifsilent
Filename: "{app}\Worknotch.exe"; Flags: nowait; Check: Relanzar

[UninstallRun]
; La app no tiene ventana que cerrar: vive junto al reloj. Se cierra antes de borrar sus archivos.
Filename: "{sys}\taskkill.exe"; Parameters: "/IM Worknotch.exe /F"; Flags: runhidden; RunOnceId: "CerrarWorknotch"
Filename: "{sys}\taskkill.exe"; Parameters: "/IM worknotch-mcp.exe /F"; Flags: runhidden; RunOnceId: "CerrarWorknotchMcp"

; Las tareas, las notas y los ajustes están en %APPDATA%\Worknotch y no se borran al desinstalar:
; son de quien usa la app, y así una reinstalación las encuentra.

[Code]
function Relanzar: Boolean;
begin
  Result := ExpandConstant('{param:relanzar|0}') = '1';
end;
