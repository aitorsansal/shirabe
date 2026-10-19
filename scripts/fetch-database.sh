#!/usr/bin/env sh
# Downloads dictionary.db3 from the GitHub Release named in database-release.txt
# into JapaneseDictionary/Resources/Raw/ and verifies its SHA-256.
set -eu

root="$(cd "$(dirname "$0")/.." && pwd)"
tag="$(sed -n 's/^tag=//p' "$root/database-release.txt")"
sha="$(sed -n 's/^sha256=//p' "$root/database-release.txt")"
target="$root/JapaneseDictionary/Resources/Raw/dictionary.db3"
url="https://github.com/aitorsansal/shirabe/releases/download/$tag/dictionary.db3"

hash_of() {
    if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | cut -d' ' -f1
    else shasum -a 256 "$1" | cut -d' ' -f1; fi
}

if [ -f "$target" ] && [ "$(hash_of "$target")" = "$sha" ]; then
    echo "dictionary.db3 is already up to date."
    exit 0
fi

echo "Downloading $url"
curl -fL "$url" -o "$target"

actual="$(hash_of "$target")"
if [ "$actual" != "$sha" ]; then
    rm -f "$target"
    echo "Checksum mismatch for dictionary.db3: expected $sha, got $actual." >&2
    exit 1
fi
echo "dictionary.db3 downloaded and verified."
