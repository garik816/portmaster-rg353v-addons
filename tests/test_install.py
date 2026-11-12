import importlib.util
import pathlib
import subprocess
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('installer', ROOT / 'install.py')
installer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(installer)
LAUNCHER = '''#!/bin/bash
gamedir=/example/stardewvalleymainline
"$DOTNET_ROOT/dotnet" "$entry_assembly"
game_status=$?
echo "status=$game_status"
'''


class InstallerTests(unittest.TestCase):
    def test_migration_and_repeat_install(self):
        legacy = '''# BEGIN PortMaster PWM rumble
portmaster_rumble_stop() { :; }
# BEGIN Stardew DMC performance
sdv_dmc_restore() { :; }
trap 'portmaster_rumble_stop; sdv_dmc_restore' EXIT
# END Stardew DMC performance
# END PortMaster PWM rumble
'''
        original = LAUNCHER.replace('"$DOTNET_ROOT', legacy + '"$DOTNET_ROOT')
        original = original.replace('game_status=$?', 'game_status=$?\nportmaster_rumble_stop\nsdv_dmc_restore')
        updated = installer.patch_launcher(original)
        self.assertEqual(installer.patch_launcher(updated), updated)
        self.assertNotIn('portmaster_rumble_stop', updated)
        self.assertNotIn('sdv_dmc_restore', updated)
        self.assertEqual(updated.count('rg353v_addons_start\n'), 1)
        self.assertIn('game_status=$?\nrg353v_addons_stop\necho', updated)

    def test_files_backups_and_configs(self):
        with tempfile.TemporaryDirectory() as temp:
            base = pathlib.Path(temp)
            launcher = base / 'StardewValleySMAPI.sh'
            launcher.write_text(LAUNCHER)
            launcher.chmod(0o755)
            game = base / 'stardewvalleymainline'
            mod = game / 'Mods/PortMasterRumble'
            mod.mkdir(parents=True)
            config = mod / 'config.json'
            config.write_text('{"ActiveDuty": 15000000}')
            dll = mod / 'PortMasterRumble.dll'
            dll.write_bytes(b'previous mod')
            backup = installer.install(launcher)
            self.assertEqual((backup / launcher.name).read_text(), LAUNCHER)
            self.assertEqual((backup / 'Mods/PortMasterRumble/PortMasterRumble.dll').read_bytes(), b'previous mod')
            self.assertEqual(config.read_text(), '{"ActiveDuty": 15000000}')
            self.assertEqual(launcher.stat().st_mode & 0o777, 0o755)
            self.assertEqual(dll.read_bytes(), (ROOT / 'mods/PortMasterRumble/PortMasterRumble.dll').read_bytes())
            first = launcher.read_bytes()
            installer.install(launcher)
            self.assertEqual(launcher.read_bytes(), first)

    def test_check_makes_no_changes(self):
        with tempfile.TemporaryDirectory() as temp:
            base = pathlib.Path(temp)
            launcher = base / 'StardewValleyMainline.sh'
            launcher.write_text(LAUNCHER)
            (base / 'stardewvalleymainline').mkdir()
            installer.install(launcher, check=True)
            self.assertEqual(launcher.read_text(), LAUNCHER)
            self.assertEqual(list((base / 'stardewvalleymainline').iterdir()), [])

    def test_unknown_launchers_rejected(self):
        for text in (LAUNCHER.replace('game_status=$?', ''), LAUNCHER + LAUNCHER,
                     LAUNCHER + 'trap custom_cleanup EXIT\n', LAUNCHER + '# BEGIN RG353V addons\n'):
            with self.assertRaises(ValueError):
                installer.patch_launcher(text)

    def test_signal_restores_dmc_and_stops_helper(self):
        with tempfile.TemporaryDirectory() as temp:
            base = pathlib.Path(temp)
            governor = base / 'governor'
            governor.write_text('dmc_ondemand\n')
            tools = base / 'tools/rg353v-addons'
            tools.mkdir(parents=True)
            helper = tools / 'rumble-helper'
            helper.write_text('#!/bin/bash\ntrap \'echo stopped > "$gamedir/stopped"; exit 0\' TERM\necho ready > "$gamedir/ready"\nwhile :; do sleep .05; done\n')
            helper.chmod(0o755)
            addon = base / 'addon.sh'
            addon.write_text((ROOT / 'tools/launcher-addon.sh').read_text().replace('/sys/class/devfreq/dmc/governor', str(governor)))
            script = '''export gamedir="$1"
source "$gamedir/addon.sh"
rg353v_addons_start
[ "$(cat "$gamedir/governor")" = performance ] || exit 1
for i in {1..40}; do [ -f "$gamedir/ready" ] && break; sleep .05; done
[ -f "$gamedir/ready" ] || exit 1
kill -TERM $$
'''
            result = subprocess.run(['bash', '-c', script, 'test', str(base)], timeout=5)
            self.assertEqual(result.returncode, 143)
            self.assertEqual(governor.read_text().strip(), 'dmc_ondemand')
            self.assertEqual((base / 'stopped').read_text().strip(), 'stopped')


if __name__ == '__main__':
    unittest.main()
