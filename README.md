# Veil

Veil is an ongoing project aiming to become a Windows desktop AI chat application.

It uses:

- .NET 10 and C#
- WPF and WebView2
- EF Core 10 and SQLite
- OpenAI API
- React 19, TypeScript, and Vite
- Tailwind CSS and Biome
- Frontend testing: Vitest, React Testing Library, user-event, jest-dom, jsdom
- Backend testing: xUnit, Microsoft.NET.Test.Sdk, xUnit Visual Studio adapter, and Coverlet

## Developer prerequisites

Install the following on a Windows development machine:

- .NET 10 SDK
- Node.js with npm
- Visual Studio with WPF support
- WebView2 Runtime on Windows versions that do not include it

For local development, create `frontend\.env` if needed and add `OPENAI_API_KEY` and `OPENAI_MODEL`.

## Run the application locally

Start the frontend:

```powershell
cd frontend
npm install
npm run dev
```

Then open `Veil.slnx` in Visual Studio and press **F5** using the `Veil` launch profile.

The React frontend runs inside the WPF WebView2 window at `http://localhost:5173`.

## Run tests

From the repository root, run:

```powershell
dotnet test .\Veil.Tests\Veil.Tests.csproj
```

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


