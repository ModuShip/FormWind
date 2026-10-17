# Third-party notices

FormWind is licensed under the [MIT License](LICENSE). It contains or uses the following third-party material.

## Included in the packages

### Tailwind CSS color palettes

`src/FormWind/Core/TwPalette.cs` and `src/FormWind/Core/TwPaletteV4.cs` contain the default color values of
[Tailwind CSS](https://github.com/tailwindlabs/tailwindcss) v3 and v4 (`theme.css` of tailwindcss 4.1.13). The class
names follow Tailwind CSS conventions. FormWind is not affiliated with or endorsed by Tailwind Labs.

```
MIT License

Copyright (c) Tailwind Labs, Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### Preset color values

The presets in `src/FormWind/Presets/` are FormWind's own component definitions. Some of their color values follow
published design tokens: the Fluent 2 brand and neutral ramps ([Fluent UI](https://github.com/microsoft/fluentui), MIT
License, Copyright (c) Microsoft Corporation), the Ant Design blue and gray palettes
([Ant Design](https://github.com/ant-design/ant-design), MIT License, Copyright (c) 2015-present Ant UED) and the
Material Design 3 baseline scheme ([material-color-utilities](https://github.com/material-foundation/material-color-utilities),
Apache License 2.0, Copyright Google LLC). No code, fonts, icons or logos of these projects are included. "Fluent",
"Ant Design" and "Material Design" are names of their owners; FormWind is not affiliated with or endorsed by them.

### Icons

`TwIcons` maps names to code points of the Segoe Fluent Icons / Segoe MDL2 Assets fonts that ship with Windows. The
fonts themselves are not included; they are used from the operating system.

## Repository only (not in the packages)

- **Outfit font** (`tests/FormWind.Tests/Fonts/`): SIL Open Font License 1.1, see
  [`OFL.txt`](tests/FormWind.Tests/Fonts/OFL.txt). Used only by the tests.
