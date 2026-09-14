# Veil

Veil is an ongoing project aiming to become a Windows desktop AI chat application.

It uses:

- React, TypeScript, and Vite
- C# and .NET 10
- WPF and WebView2
- SQLite and Entity Framework Core
- EF Core migrations for database versioning

## Developer prerequisites

Install the following on a Windows development machine:

- .NET 10 SDK
- Node.js with npm
- Visual Studio with WPF support
- WebView2 Runtime on Windows versions that do not include it

The repository `.env` file is for local development only. It may contain `OPENAI_API_KEY` and `OPENAI_MODEL`.

## Run the application locally

Start the frontend:

```powershell
cd frontend
npm install
npm run dev
```

Then open `Veil.slnx` in Visual Studio and press **F5** using the `Veil` launch profile.

The React frontend runs inside the WPF WebView2 window at `http://localhost:5173`.

## Build the standalone executable

From the repository root, run:

```powershell
dotnet publish .\Veil\Veil.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\publish\win-x64
```

The final executable is:

```text
publish\win-x64\Veil.exe
```

## First-run application setup

When the published executable starts for the first time, enter the OpenAI API key and model in Veil. The application stores both values encrypted for the current Windows user and reuses them on later launches.


