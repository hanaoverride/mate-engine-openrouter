# STOP. DO NOT SKIM OVER IT.

# What is this repository?

This is a fork of the original Mate-Engine repository, modified to work with OpenRouter models.

# What is Mate-Engine?

Check out the original repository:
![Mate-Engine](https://github.com/shinyflvre/Mate-Engine)

# This repository is under development

yes, I know, the code is a mess. I wanted it so bad to work that I just hacked it together. I will clean it up later. Even though I have a plan on releasing it, I still need to test it more.

# Why OpenRouter?

When it comes to the realm of AI character chatbots, you might need to use various models to achieve the desired performance. OpenRouter provides a unified API to access multiple models, making it easier to switch between them without changing your codebase significantly.

# How do I even run this fork?

1. Clone the repo, open the Unity project. Let Unity import **everything** before you mash any buttons.
2. Hit play inside the sample scene. If you just get a sad, silent mascot, your OpenRouter creds probably aren't loaded. Keep reading.

# Setting up OpenRouter the lazy way

- I kept PlayerPrefs support so legacy builds don't implode, but the real source of truth is `settings.json` under `AppData/LocalLow/Shinymoon/MateEngineX/`.
- Boot the game → Settings → OpenRouter panel → hit `Edit` button to open the file automatically.
- Tweak these fields:
	- `openRouterEnabled`: flip it to `true` to make the game use the OpenRouter-powered models.
	- `openRouterApiKey`: it's literally your API key—if chat doesn't work, fill it in.
	- `openRouterModel`: model IDs such as `deepseek/deepseek-chat-v3.1` or `openai/gpt-5`.
	- `openRouterTemperature`, `openRouterMaxTokens`, `openRouterStreaming`, `openRouterDebug`: mapped 1:1 to the sliders/toggles in the UI.
- Save the file and jump back into Play Mode—the changes take effect immediately. If you hand-edit the JSON, the UI will reload those values the next time it boots.

# If you prefer environment variables

Yes, env vars still work. Drop `OpenRouter_API_Key`, `OpenRouter_Model`, `OpenRouter_Enabled`, and friends into your system/build environment; on first launch they'll be copied into PlayerPrefs. After that `settings.json` is the source of truth, so update the file if you want those tweaks to stick around.

# Known rough edges (a.k.a. read before pinging me)

- This fork ships with streaming (SSE) responses. If nothing trickles out character-by-character, double-check the streaming toggle.
- The `settings.json` shortcut only opens on Windows/macOS/Linux. Everywhere else you'll just get a polite failure message.
- All UI fields are still legacy `UnityEngine.UI` bits. If you migrate to TextMeshPro, you'll have to redo the bindings yourself.
- The codebase is still very much under construction. This README will keep changing—if something looks broken, start here.