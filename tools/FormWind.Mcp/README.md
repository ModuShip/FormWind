# FormWind.Mcp

An [MCP](https://modelcontextprotocol.io) server for [FormWind](https://github.com/ModuShip/FormWind), the Tailwind-style styling layer for WinForms. It lets coding agents (Claude Code, GitHub Copilot, Cursor …) check the classes they write and see the result, instead of guessing.

## Install

```bash
dotnet tool install -g FormWind.Mcp --prerelease
```

Requires Windows and the .NET 8 runtime (or newer). Add the server to your agent, for example in `.mcp.json` at the root of your project for Claude Code:

```json
{
  "mcpServers": {
    "formwind": { "command": "formwind-mcp" }
  }
}
```

or with the CLI: `claude mcp add formwind -- formwind-mcp`.

## Tools

| Tool | What it does |
| --- | --- |
| `validate_classes` | Lists the unknown classes in a class string, each with a "did you mean" suggestion (`hovr:bg-blu-600` → `hover:bg-blue-600`), and the components used. |
| `explain_classes` | Resolves the classes to the property values they set (BackColor, ForeColor, Font, Padding, Radius …) in the base state and in every variant (dark, hover, disabled, parts such as `header:`); `states` resolves combinations such as `dark:hover`. |
| `list_components` | The theme's components (`btn-primary`, `card`, `input` …) with what they expand to, and the AutoStyle mapping from control types to components. |
| `list_vocabulary` | The class patterns FormWind recognizes, by category, with the theme's color names, shades and variants. |
| `render` | Draws a control (standard or `Tw*`, optionally with children) with the classes and returns one PNG: a row per mode (light, dark) and a column per state (base, hover, focus, active, disabled, checked, invalid). `stateTarget` applies the states to one child instead (a hovered `group` container also shows its children's `group-hover:` classes). Without `width` and `height`, buttons, labels and check boxes size to their text and padding classes. |

Every tool returns structured content with an output schema; `render` also returns the image, and its summary gives the image layout (label sizes, cell size, control offset) so a frame can be located or cropped.

Errors are tool results with `isError: true` whose text is a JSON object: `{"code": "unknown_preset", "message": "Unknown preset 'bootstrap'.", "hint": "Use tailwind, fluent, antd or material."}`. Codes: `unknown_preset`, `theme_not_found`, `invalid_theme`, `unknown_state`, `unknown_mode`, `unknown_category`, `unknown_control_type`, `invalid_state_target`, `timeout`, `render_failed`.

Every tool takes an optional `preset` (`tailwind`, `fluent`, `antd`, `material`) and `themeFile`. Without them the server uses `tw.config.json` in its working folder (the folder the agent starts it in), or the default theme.

## Resources

- `formwind://llms.txt`: FormWind for coding agents (approaches, components, classes, variants, rules).
- `formwind://presets/{name}`: a preset in the `tw.config.json` format.

## Safety

The server is read-only: no tool writes files, and no tool runs code from your application. `render` draws controls from WinForms and FormWind only, off screen, one at a time, with a 30-second limit.
