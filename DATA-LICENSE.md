# Dictionary database license

The bundled database, `JapaneseDictionary/Resources/Raw/dictionary.db3`, and any database produced by `JapaneseDictionary.DataBuilder`, is derived from the datasets listed below. It is distributed under the [Creative Commons Attribution-ShareAlike 4.0 International license (CC BY-SA 4.0)](https://creativecommons.org/licenses/by-sa/4.0/legalcode).

If you redistribute the database, or a database built from it, you must credit the sources below, indicate any changes you made, and release it under CC BY-SA 4.0 (or a license CC lists as compatible). The MIT license in `LICENSE` applies to the source code only, not to this data.

## Sources

### JMdict and KANJIDIC2

JMdict (dictionary entries, readings, senses and their English, Spanish, French and German glosses, word classes) and KANJIDIC2 (kanji readings, meanings, grades, frequency) are the property of the [Electronic Dictionary Research and Development Group](https://www.edrdg.org/) and are used in conformance with the Group's [licence](https://www.edrdg.org/edrdg/licence.html), which is CC BY-SA 4.0.

- JMdict: <https://www.edrdg.org/wiki/index.php/JMdict-EDICT_Dictionary_Project>
- KANJIDIC: <https://www.edrdg.org/wiki/index.php/KANJIDIC_Project>

Acknowledgement text for the app: "This application uses the JMdict/EDICT and KANJIDIC dictionary files. These files are the property of the Electronic Dictionary Research and Development Group, and are used in conformance with the Group's licence."

### KanjiVG

Stroke order data comes from [KanjiVG](https://kanjivg.tagaini.net/), copyright Ulrich Apel, licensed under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). CC BY-SA 3.0 allows adaptations to be distributed under a later version of the same license, which is why the combined database is released under CC BY-SA 4.0.

### Tatoeba

Example sentences and their English, Spanish, French and German translations come from [Tatoeba](https://tatoeba.org/) and its contributors, licensed under [CC BY 2.0 FR](https://creativecommons.org/licenses/by/2.0/fr/). Each sentence in the database keeps its Tatoeba sentence id, so it can be traced back to its page and author on tatoeba.org.

### Kanjium (pitch accent)

Pitch accent data comes from the `accents.txt` file of [Kanjium](https://github.com/mifunetoshiro/kanjium) by Uros O., licensed under CC BY-SA 4.0 (full text in [`licenses/CC-BY-SA-4.0.txt`](licenses/CC-BY-SA-4.0.txt)). Kanjium's list is matched to JMdict entries by spelling and reading.

### JLPT levels

The JLPT levels for words and kanji are Jonathan Waller's [JLPT resources](https://www.tanos.co.uk/jlpt/) (tanos.co.uk), released under a Creative Commons Attribution license. They reach the database through two community conversions of those lists:

- words: [stephenmk/yomitan-jlpt-vocab](https://github.com/stephenmk/yomitan-jlpt-vocab) (CC BY-SA 4.0), which adds JMdict entry ids to Waller's word lists;
- kanji: [Bluskyo/JLPT_Vocabulary](https://github.com/Bluskyo/JLPT_Vocabulary) (MIT, see `THIRD-PARTY-NOTICES.md`), whose `JLPT_kanji_ALL.json` is built from Waller's kanji lists.

These levels are unofficial community estimates: the Japan Foundation and JEES have published no official vocabulary or kanji lists since the test was revised in 2010. A word or kanji listed at several levels keeps the easiest.

## Changes made

The source files are parsed, filtered and restructured into a single SQLite schema. Example sentences are linked to JMdict entries using Tatoeba's Japanese indices. Spanish, French and German sentence translations are the sentences Tatoeba links directly to the Japanese sentence (the lowest sentence id when there are several). Spanish, French and German glosses are kept as separate lists per language, since JMdict does not align them with the English senses. Pitch accents are stored per reading of an entry as Kanjium lists them (several accents for a reading are kept in source order; part-of-speech tags that a few Kanjium rows put before an accent number are dropped). JLPT levels are stored as an N5 to N1 number on words and kanji; the old four-level JLPT number KANJIDIC2 ships is kept separately and unchanged. Entry text is otherwise kept as published.
