# Audio Studio (Ses Stüdyosu)

A Windows desktop voice changer and soundboard built with C# and Windows Forms. Process your microphone, play sounds, route audio into other applications, or try optional local AI voice conversion.

[Download the latest release](https://github.com/Ege-roze/ses-studyosu/releases/latest) · [MIT license](LICENSE) · [Türkçe kullanım kılavuzu](AI-SES.md)

## Features

- Live microphone processing with pitch, distortion, echo, and a three-band equalizer.
- Radio, cave, and anime effect presets.
- A searchable sound library with playback mixed into the outgoing audio.
- VB-CABLE setup and an outgoing-audio listening test.
- Optional local AI voice conversion with nine separately downloadable voice models.
- Model management: download status, individual model sizes, total storage usage, and deletion with confirmation. Deleted models can be downloaded again.
- An independent AI microphone test that plays through your selected headphones or speakers without starting the main stream or installing VB-CABLE.
- Glass and classic iTunes/iPod-inspired appearance packs, custom backgrounds, and an adjustable glass effect.
- Configurable microphone/output devices, close-to-tray behavior, startup in the tray, and startup with Windows.
- Turkish and English interfaces, with offline privacy, usage, and third-party notices.

## Download and run

1. Open [Releases](https://github.com/Ege-roze/ses-studyosu/releases) and download **Ses-Studyosu.zip** from the release assets. GitHub's automatic **Source code** archives are for developers.
2. Extract the entire ZIP to a folder. Keep the executable, configuration, DLLs, and license documents together.
3. Run **ses uyg.exe**.

The application requires Windows and **.NET Framework 4.8**. Classic audio effects do not require the AI engine. The optional AI integration currently targets **64-bit Windows with an NVIDIA/CUDA-compatible GPU**; this distribution does not provide a CPU-only or AMD AI engine.

The app ZIP is approximately **3.2 MB**. The AI engine and voice weights are downloaded from inside the application when needed. The shared engine download is approximately **3.3 GB**, with around **12 GB of free space** required during installation. Each selectable voice model is approximately **55–67 MB**. Supporting weights may also be downloaded by the engine.

## Use with Discord, games, or streaming software

1. Install VB-CABLE from the application's setup section if you need to send processed audio to another application. The official installer asks for administrator approval and may require a restart.
2. Choose your microphone and **CABLE Input** as the main output, then click **Start streaming**.
3. Select **CABLE Output** as the microphone/input in the receiving application. Keep your headphones as its listening output.
4. Adjust the effects, play sounds, or select an AI model and click **Enable AI voice**.

The main effects, AI, and sound-library panels remain locked until streaming starts. Setup, settings, personalization, and the test window are available before streaming.

Use headphones to avoid feedback. The main window's close button hides the app in the system tray by default; use **Exit application** to stop it completely. You can change this behavior in Settings.

## AI voices and model management

The available choices are Tsukuyomi-chan, Amitaro, Amitaro 16 kHz, Amitaro 16 kHz No F0, and AISO Howatto, Kakkoii, Sarasara, Sittori, and Syakitto. They come from the pinned `wok000/vcclient_model` catalog. All choices share the same VCClient/RVC engine.

Select a voice and use **Download selected voice** to install only that model. Enabling or testing a missing voice also starts its download. Downloads support cancellation/resume and SHA-256 verification.

**Model management** shows the number of downloaded voices and their combined disk usage. Partial downloads are included and identified separately; the shared engine is excluded from this total. **Delete selected model** removes that voice's files and any partial download after confirmation. Other models and the shared engine are retained. Model operations are locked while the AI session or another operation is using them.

To manage or test a voice while the main stream is stopped, open **Test audio**, choose a model, microphone, and listening device, and click **Start AI test**. This route does not require VB-CABLE.

Most voices use 300 ms audio blocks. AISO voices use 2040 ms blocks and therefore have noticeably higher conversational latency. Pitch adjustment is unavailable for No F0 and AISO models. Output quality and latency depend on the model, input, and hardware.

## Privacy and local storage

Classic processing and AI inference run on your computer. The app connects to its local AI engine over HTTPS at `127.0.0.1:18991`; it does not send microphone audio to a cloud inference service. Internet access is used for downloads, catalog retrieval, and external links.

The bundled integration has a known limitation: the upstream VCClient engine listens on all IPv4 interfaces and has no separate user authentication. Reachability depends on your firewall and network; local inference does not imply network isolation. See the [Privacy Policy](Legal/privacy-policy.en.txt) for details.

| Data | Default location |
| --- | --- |
| Device, startup, and appearance preferences | `%LOCALAPPDATA%\SesStudyosu` |
| Downloaded AI engine | `%LOCALAPPDATA%\SesStudyosu\AiRuntime` |
| Downloaded voice models | `%LOCALAPPDATA%\SesStudyosu\AiVoices` |
| Sound-library names and file paths | `soundboard_config.xml` beside the executable |

Existing portable/development installations may use an `AiRuntime` directory beside or above the application and its `ses_models` folder. Neither personal settings nor downloaded engines/models are included in this repository or the release ZIP.

## Build from source

Use Visual Studio with the **.NET desktop development** workload and the **.NET Framework 4.8 targeting pack**, or equivalent MSBuild tools. Run these commands in a Developer PowerShell with `msbuild` on `PATH`:

```powershell
git clone https://github.com/Ege-roze/ses-studyosu.git
cd ses-studyosu
msbuild "ses uyg.sln" /t:Restore /p:RestorePackagesConfig=true
msbuild "ses uyg.csproj" /t:Build /p:Configuration=Release
```

Alternatively, open `ses uyg.sln` in Visual Studio, restore NuGet packages, and build Release. The executable and dependencies are written to `bin\Release`.

To produce the distributable ZIP, including the English README, MIT license, and third-party notices:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-App.ps1 -ForPublication
```

Output: `dist\Ses-Studyosu.zip`. The packaging script excludes downloaded engines, voice weights, test executables, and personal settings. Publishers of a fork should set their own contact details in `Legal/publisher.json` before generating the documents and rebuilding.

## Validation

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Run-Checks.ps1 -Configuration Release
```

The default suite covers audio processing, settings, localization, themes, AI queues, installation, model storage/management, and bundled documents. It uses synthetic audio and isolated test data; it does not start physical microphone capture. Local HTTP fixtures may require a normal Windows session rather than a restricted sandbox.

Optional real-engine smoke test, using the installed engine and a synthetic signal:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Run-Checks.ps1 -Configuration Release -AiRuntime
```

## License and credits

The original Audio Studio application code and documentation are released under the **[MIT License](LICENSE)**, copyright 2026 Ege.

Third-party components keep their own licenses; the MIT license does not relicense them:

- **NAudio** and the Microsoft NuGet dependencies: their original notices are included under [`Legal/licenses`](Legal/licenses).
- **VB-CABLE** by VB-Audio / Vincent Burel: donationware, distributed here as the unmodified standard driver package. See the [vendor attribution](ThirdParty/VBCable/NOTICE.md) and [donation/licensing page](https://vb-audio.com/Services/licensing.htm).
- **VCClient / w-okada voice-changer** and its dependencies: downloaded separately, with their own notices.
- **AI voice models and supporting weights**: separately licensed, with model-specific usage restrictions. Read the selected voice's source/terms link and the [Voice Model Terms](Legal/model-terms.en.txt) before use. They are not covered by this project's MIT license.

See [Third-Party Notices](Legal/third-party-notices.en.txt) for component details.

## Support

Report reproducible issues through [GitHub Issues](https://github.com/Ege-roze/ses-studyosu/issues). Include the app version, Windows version, and relevant device/model names; avoid uploading private recordings or unredacted logs.

Publisher: **Ege** · Contact: **egeroze4@gmail.com**
