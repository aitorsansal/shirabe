# Third-party notices

This project includes or depends on the components below. Each stays under its own license. For the dictionary data (JMdict, KANJIDIC2, KanjiVG, Tatoeba, Kanjium, JLPT levels) see `DATA-LICENSE.md` (this includes the Spanish, French and German JMdict glosses and Tatoeba sentence translations). The CC BY-SA 4.0 license text is in [`licenses/CC-BY-SA-4.0.txt`](licenses/CC-BY-SA-4.0.txt).

## Pitch accent and JLPT data

- Pitch accents: [Kanjium](https://github.com/mifunetoshiro/kanjium) by Uros O., CC BY-SA 4.0.
- JLPT levels: Jonathan Waller's [JLPT resources](https://www.tanos.co.uk/jlpt/) (Creative Commons Attribution), via [stephenmk/yomitan-jlpt-vocab](https://github.com/stephenmk/yomitan-jlpt-vocab) (CC BY-SA 4.0) for words and [Bluskyo/JLPT_Vocabulary](https://github.com/Bluskyo/JLPT_Vocabulary) for kanji. The levels are unofficial community estimates.

Bluskyo/JLPT_Vocabulary is licensed under the MIT License:

```
MIT License

Copyright (c) 2026 Bluskyo

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

## Handwriting recognition model

`JapaneseDictionary/Resources/Raw/Models/char_classifier.onnx` and `char_classifier_labels.txt` come from [DaKanji Single Kanji Recognition](https://github.com/CaptainDario/DaKanji-Single-Kanji-Recognition).

Character recognition powered by machine learning from Dariyooo (DaAppLab).

The project is licensed under the MIT License:

```
MIT License

Copyright (c) 2021 CaptainDario

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

The model was trained on the ETL Character Database and the KanjiVG dataset (credited by the DaKanji project). Training data is not included in this repository.

- ETL Character Database: Electrotechnical Laboratory, Japanese Technical Committee for Optical Character Recognition, ETL Character Database, 1973-1984. Distributed by AIST: <https://etlcdb.db.aist.go.jp/>

## Fonts

| Font | Copyright | License |
|---|---|---|
| [Archivo](https://github.com/Omnibus-Type/Archivo) | 2020 The Archivo Project Authors | SIL Open Font License 1.1, [`licenses/OFL-Archivo.txt`](licenses/OFL-Archivo.txt) |
| [Noto Serif JP](https://fonts.google.com/noto/specimen/Noto+Serif+JP) (the Bold weight is subset to the 辞書 wordmark) | 2017-2024 Adobe | SIL Open Font License 1.1, [`licenses/OFL-NotoSerifJP.txt`](licenses/OFL-NotoSerifJP.txt) |
| [Material Symbols Sharp](https://github.com/google/material-design-icons) (static instances, subset to the icons the app uses) | 2026 Google LLC | Apache License 2.0, [`licenses/Apache-2.0.txt`](licenses/Apache-2.0.txt) |

## Libraries

| Package | License |
|---|---|
| [Microsoft.ML.OnnxRuntime](https://github.com/microsoft/onnxruntime) | MIT |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT |
| [sqlite-net-pcl](https://github.com/praeclarum/sqlite-net) | MIT |
| [SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw) | Apache License 2.0, [`licenses/Apache-2.0.txt`](licenses/Apache-2.0.txt) |
| [.NET MAUI](https://github.com/dotnet/maui) | MIT |
