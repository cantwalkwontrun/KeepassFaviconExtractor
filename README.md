---
lang: en-GB
---

# KeePass Favicon Extractor

## Maintenance fork

Version **1.1.5.2** includes the HTML-response, SVG resource and cancellation fixes described in [REVIEW.md](REVIEW.md). It also uses this fork's update feed. The first public fork release packages the plugin and its required dependencies for Windows with .NET Framework 4.8; verification used the official KeePass 2.61 reference and both x64 and x86 test hosts.

Download the ZIP and `SHA256SUMS.txt` from [this fork's releases](https://github.com/cantwalkwontrun/KeepassFaviconExtractor/releases). Verify the ZIP checksum, close KeePass and retain one working plugin copy locally before replacing it. The package contains a `FaviconExtractor` folder for KeePass's `Plugins` directory. Keep only one copy of this plugin active; no PLGX is supplied.

This is a maintenance fork of [the original project](https://github.com/zeynelozturk/KeepassFaviconExtractor). Release DLLs are unsigned. The real-application workflow and the exact website response behind the reported failure remain unverified; the tests use synthetic inputs.

## Original features

A KeePass 2 plugin that finds and downloads website favicons. The extension
uses multiple discovery methods, therefore it has a higher likelihood of finding a favicon.

![Favicon Extractor screenshot](Assets/FaviconExtractor_screenshot.webp)
![Favicon Extractor screenshot](Assets/FaviconExtractor_screenshot_icon_found.webp)

## Highlights

- Entry right-click and main-menu integration in KeePass.
- Multi-source favicon discovery with ranked candidate selection: HTML parsing for fetching
  favicon (SVG, PNG, WEBP, ICO supported); fallback to multiple external services including
  Google, DuckDuckGo.
- Can download Android app icons (if AndroidApp1 or AndroidApp fields are present in the entry).
- 128x128 is the default size. Smaller icons aren't upscaled, but larger icons are downscaled to 128x128.
- Live extraction status and diagnostics windows.
- Multiple selection support for batch extraction.
- "Download missing icons" feature for eligible entries in a database.
- Guard against storing duplicate icons in the KeePass database.
- Diagnostics window allows you to check if extension is working properly.

## Installation

- Download the latest version and checksum from [the fork releases](https://github.com/cantwalkwontrun/KeepassFaviconExtractor/releases/latest).
- Extract the .zip file into your KeePass `Plugins` directory. This will create a FaviconExtractor subdirectory
  containing the plugin and its dependencies.
- Restart KeePass.

## Usage

- Right click an entry in KeePass and select "Extract website favicon" from the context menu.
- The plugin will attempt to find and download a favicon for the entry's URL,
convert it to a suitable format. If a favicon is found, it will be assigned to the entry.

![Favicon Extractor context menu](Assets/FaviconExtractor_context_menu.webp)

Note that this extension will change the icon of entry without confirmation.

We do not modify or delete previously stored icons.

## Network and privacy behavior

- The plugin makes outbound HTTP/HTTPS requests to the target site's URL to discover icons.
- If direct discovery fails, it may query external providers (for example Google, DuckDuckGo,
  Favicone, Vemetric, Favicon.im, and Hunter logo endpoint).
- External providers receive host/domain-based values (host and sometimes parent domain),
  not the full entry URL path/query.
- Redirects that end on private or loopback addresses can be blocked with
  `EnforcePrivateAddressBlocking` (default is compatibility-first).
- Native WEBP loading prefers plugin-local `native/x64` and `native/x86`; legacy broad path
  fallback is controlled by `AllowLegacyNativeLibrarySearchFallback`.
- Some response reads use explicit size caps (`MaxHtmlReadBytes`, `MaxPlaceholderHashReadBytes`)
  to reduce memory-risk from oversized responses.

## Licensing and Third-Party Notices

- This project is licensed under GPL-3.0. See [LICENSE](LICENSE).
- Third-party component notices and license texts are provided in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
- Release ZIP artifacts include both files.

## Contact

For maintenance-fork bug reports, use the fork's issue tracker with a synthetic reproduction. Do not post real passwords, databases, private URLs, account details, screenshots or unredacted logs. The original author maintains the upstream project separately.

For build and development notes, see [DEVELOPMENT.md](DEVELOPMENT.md).
