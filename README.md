# What is this repository?

This is a fork of the original Mate-Engine repository, modified to work with OpenRouter models.

# What is Mate-Engine?

Check out the original repository:
![Mate-Engine](https://github.com/shinyflvre/Mate-Engine)

# Why OpenRouter?

When it comes to the realm of AI character chatbots, you might need to use various models to achieve the desired performance. OpenRouter provides a unified API to access multiple models, making it easier to switch between them without changing your codebase significantly.

# Setting up OpenRouter

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

# Known rough edges

- This fork ships with streaming (SSE) responses. If nothing trickles out character-by-character, double-check the streaming toggle.
- The `settings.json` shortcut only opens on Windows/macOS/Linux. Everywhere else you'll just get a polite failure message.
- All UI fields are still legacy `UnityEngine.UI` bits. If you migrate to TextMeshPro, you'll have to redo the bindings yourself.

# How to run it
- run the game for the first time, right-click the chararcter, and select settings icon.
- scroll until you see "OPENROUTER SETTINGS SECTIONS". click "EDIT" button to open the settings file.
- fill in your OpenRouter API key, and optionally change the model to something else.
- save the file.
- next, click "EDIT SYSTEM PROMPT" button to open the system prompt file.
- edit the system prompt to your liking, save the file.
- close the game, and run it again.
