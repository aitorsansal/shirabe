# Dictionary database license

The bundled database, `JapaneseDictionary/Resources/Raw/dictionary.db3`, and any database produced by `JapaneseDictionary.DataBuilder`, is derived from the datasets listed below. It is distributed under the [Creative Commons Attribution-ShareAlike 4.0 International license (CC BY-SA 4.0)](https://creativecommons.org/licenses/by-sa/4.0/legalcode).

If you redistribute the database, or a database built from it, you must credit the sources below, indicate any changes you made, and release it under CC BY-SA 4.0 (or a license CC lists as compatible). The MIT license in `LICENSE` applies to the source code only, not to this data.

## Sources

### JMdict and KANJIDIC2

JMdict (dictionary entries, readings, senses, word classes) and KANJIDIC2 (kanji readings, meanings, grades, frequency) are the property of the [Electronic Dictionary Research and Development Group](https://www.edrdg.org/) and are used in conformance with the Group's [licence](https://www.edrdg.org/edrdg/licence.html), which is CC BY-SA 4.0.

- JMdict: <https://www.edrdg.org/wiki/index.php/JMdict-EDICT_Dictionary_Project>
- KANJIDIC: <https://www.edrdg.org/wiki/index.php/KANJIDIC_Project>

Acknowledgement text for the app: "This application uses the JMdict/EDICT and KANJIDIC dictionary files. These files are the property of the Electronic Dictionary Research and Development Group, and are used in conformance with the Group's licence."

### KanjiVG

Stroke order data comes from [KanjiVG](https://kanjivg.tagaini.net/), copyright Ulrich Apel, licensed under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). CC BY-SA 3.0 allows adaptations to be distributed under a later version of the same license, which is why the combined database is released under CC BY-SA 4.0.

### Tatoeba

Example sentences and their translations come from [Tatoeba](https://tatoeba.org/) and its contributors, licensed under [CC BY 2.0 FR](https://creativecommons.org/licenses/by/2.0/fr/). Each sentence in the database keeps its Tatoeba sentence id, so it can be traced back to its page and author on tatoeba.org.

## Changes made

The source files are parsed, filtered and restructured into a single SQLite schema. Example sentences are linked to JMdict entries using Tatoeba's Japanese indices. Entry text is otherwise kept as published.
