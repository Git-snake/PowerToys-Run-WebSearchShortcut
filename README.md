# PowerToys-Run-WebSearchShortcut

This is a simple [PowerToys Run](https://docs.microsoft.com/en-us/windows/powertoys/run) plugin for performing web searches.

This plugin enables users to perform web searches using specific keywords. 

## Preview

![search-1](./ScreenShots/search-1.png)

![search-2](./ScreenShots/search-2.png)

## Requirements

- PowerToys minimum version 0.79.0

## Installation

- Download the [latest release](https://github.com/Daydreamer-riri/PowerToys-Run-WebSearchShortcut/releases/) by selecting the architecture that matches your machine: `x64` (more common) or `ARM64`
- Close PowerToys
- Extract the archive to `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins`
- Open PowerToys

## Config

- Open config file:

![config](./ScreenShots/config.png)

- Inside the config file, you can add your desired search engines. The key is the display name of the search engine, and the `url` property is the URL template for performing the search. Use `%s` as a placeholder for the search query.

![config-file](./ScreenShots/config-file.png)

- Run `reload`:

![reload](./ScreenShots/reload.png)

## License

[MIT](./LICENSE) License © 2023 [Riri](https://github.com/Daydreamer-riri)
