"""Fetch a pinned model at build time; app photos never leave the device."""
import argparse
import hashlib
import http.client
import json
from pathlib import Path
import plistlib
import urllib.request
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'assets/object-removal'
SPEC = json.loads((FOLDER / 'manifest.json').read_text())

def validate_file(path):
    path = Path(path)
    if not path.is_file() or path.stat().st_size != SPEC['bytes']:
        raise ValueError('Object removal model missing or wrong size')
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    if digest.hexdigest() != SPEC['sha256']:
        raise ValueError('Object removal model checksum mismatch')

def validate_bundled_model(bundle):
    validate_file(Path(bundle) / SPEC['file'])
    if (Path(bundle) / SPEC['license']).read_bytes() != (FOLDER / SPEC['license']).read_bytes():
        raise ValueError('Object removal model licence missing or changed')
    if (Path(bundle) / 'NOTICE-AOT-GAN.txt').read_bytes() != (FOLDER / 'NOTICE.txt').read_bytes():
        raise ValueError('Object removal model attribution missing or changed')

def fetch():
    target = ROOT / 'artifacts/native/models' / SPEC['file']
    try:
        validate_file(target)
        return
    except ValueError:
        pass
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_suffix('.download')
    try:
        for attempt in range(3):
            try:
                with urllib.request.urlopen(SPEC['url'], timeout=60) as source, temporary.open('wb') as output:
                    total = 0
                    while block := source.read(1024 * 1024):
                        total += len(block)
                        if total > SPEC['bytes']:
                            raise ValueError('Object removal model download exceeds declared size')
                        output.write(block)
                if total != SPEC['bytes']:
                    raise OSError('Incomplete object removal model download')
                break
            except (OSError, http.client.IncompleteRead):
                if attempt == 2:
                    raise
                time.sleep(attempt + 1)
        validate_file(temporary)
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)

def check():
    licence = FOLDER / SPEC['license']
    if SPEC['id'] != 'aotgan-512-v1' or SPEC['edge'] != 512 or SPEC['input'] != 'x_1' or SPEC['output'] != 'var_915':
        raise ValueError('Object removal model contract mismatch')
    if hashlib.sha256(licence.read_bytes()).hexdigest() != SPEC['licenseSha256']:
        raise ValueError('Object removal model licence changed')
    host = ROOT / 'src/ControlStudio.App'
    with (host / 'Platforms/iOS/Info.plist').open('rb') as stream:
        if plistlib.load(stream).get('ControlStudioObjectRemovalModel') != SPEC['id']:
            raise ValueError('Object removal host model declaration missing or changed')
    resources = ET.parse(host / 'ControlStudio.App.csproj').findall('.//BundleResource')
    for name, path in {
        SPEC['file']: '$(NativeBuildRoot)/models/' + SPEC['file'],
        SPEC['license']: '../../assets/object-removal/' + SPEC['license'],
        'NOTICE-AOT-GAN.txt': '../../assets/object-removal/NOTICE.txt',
    }.items():
        matches = [item for item in resources if item.get('LogicalName') == name]
        if len(matches) != 1 or matches[0].get('Include') != path:
            raise ValueError('Object removal host resource missing or changed: ' + name)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fetch', action='store_true')
    args = parser.parse_args()
    check()
    if args.fetch:
        fetch()
    print('Object removal model configuration verified.' if not args.fetch else 'Pinned object removal model verified.')
