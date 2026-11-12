#!/usr/bin/env python3
"""Install the tested RG353V Stardew addons without replacing user configs."""
import argparse
import datetime
import pathlib
import re
import shutil
import subprocess
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent


def patch_launcher(text):
    # Migrate the original rumble snippet, including its nested DMC fix.
    for name in ('PortMaster PWM rumble', 'RG353V addons'):
        begin, end = '# BEGIN ' + name, '# END ' + name
        if text.count(begin) != text.count(end) or text.count(begin) > 1:
            raise ValueError('Ambiguous addon markers; launcher left unchanged')
        text = re.sub(r'^' + re.escape(begin) + r'\n.*?^' + re.escape(end) + r'\n?', '', text, flags=re.M | re.S)
    # Remove only cleanup calls owned by previous addon versions.
    text = re.sub(r'^(?:portmaster_rumble_stop|sdv_dmc_restore|rg353v_addons_stop)\n', '', text, flags=re.M)
    if '# BEGIN Stardew DMC performance' in text:
        raise ValueError('Unrecognized standalone DMC integration; launcher left unchanged')
    if re.search(r'^\s*trap\s', text, flags=re.M):
        raise ValueError('Custom signal traps found; manual integration required')
    command = '"$DOTNET_ROOT/dotnet" "$entry_assembly"'
    if len(re.findall(r'^' + re.escape(command) + r'$', text, flags=re.M)) != 1:
        raise ValueError('Expected exactly one mainline game launch command')
    if len(re.findall(r'^game_status=\$\?$', text, flags=re.M)) != 1:
        raise ValueError('Expected exactly one game exit status assignment')
    block = '''# BEGIN RG353V addons
source "$gamedir/tools/rg353v-addons/launcher-addon.sh"
rg353v_addons_start
# END RG353V addons
'''
    text = text.replace(command, block + command)
    return text.replace('game_status=$?', 'game_status=$?\nrg353v_addons_stop')


def install(launcher, check=False, root=ROOT):
    launcher = pathlib.Path(launcher).resolve()
    game = launcher.parent / 'stardewvalleymainline'
    if not launcher.is_file() or not game.is_dir():
        raise ValueError('Expected an existing mainline launcher beside stardewvalleymainline/')
    text = patch_launcher(launcher.read_text())
    files = {
        root / 'tools/rumble-helper': game / 'tools/rg353v-addons/rumble-helper',
        root / 'tools/launcher-addon.sh': game / 'tools/rg353v-addons/launcher-addon.sh',
    }
    for mod in ('PortMasterRumble', 'PortMasterTouch'):
        for source in (root / 'mods' / mod).iterdir():
            if source.name in ('manifest.json', mod + '.dll'):
                files[source] = game / 'Mods' / mod / source.name
    for source in files:
        if not source.is_file():
            raise ValueError('Missing package file: ' + str(source))
    for mod in ('PortMasterRumble', 'PortMasterTouch'):
        if len([p for p in files if p.parent.name == mod]) != 2:
            raise ValueError('Incomplete mod package: ' + mod)
    subprocess.run(['bash', '-n'], input=text, text=True, check=True)
    print('Launcher:', launcher)
    if check:
        print('Validation passed; no files changed.')
        return None
    backup = game / 'addon-backups' / datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    backup.mkdir(parents=True)
    shutil.copy2(launcher, backup / launcher.name)
    for target in files.values():
        if target.exists():
            saved = backup / target.relative_to(game)
            saved.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(target, saved)
    # All existing targets are backed up before the first replacement.
    for source, target in files.items():
        target.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=target.parent, delete=False) as stream:
            staged = pathlib.Path(stream.name)
        try:
            shutil.copyfile(source, staged)
            staged.chmod(0o755 if source.name == 'rumble-helper' else 0o644)
            staged.replace(target)
        finally:
            staged.unlink(missing_ok=True)
    with tempfile.NamedTemporaryFile(mode='w', dir=launcher.parent, delete=False) as stream:
        stream.write(text)
        staged = pathlib.Path(stream.name)
    try:
        staged.chmod(launcher.stat().st_mode & 0o777)
        staged.replace(launcher)
    finally:
        staged.unlink(missing_ok=True)
    print('Installed. Configs and saves preserved. Backup:', backup)
    print('Restart Stardew Valley to activate the addons.')
    return backup


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('launcher', type=pathlib.Path)
    parser.add_argument('--check', action='store_true', help='validate without writing files')
    args = parser.parse_args()
    try:
        install(args.launcher, args.check)
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        parser.exit(1, str(error) + '\n')
