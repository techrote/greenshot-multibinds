/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 *
 * For more information see: https://getgreenshot.org/
 * The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Collections.Generic;
using Dapplo.Windows.Input.Enums;
using Greenshot.Base.Core;
using Greenshot.Base.Recipes.Triggers;
using log4net;
using Greenshot.Base.Threading;

namespace Greenshot.Recipes.Triggers
{
    /// <summary>
    /// Trigger that fires a recipe when a specific global keyboard hotkey is pressed.
    /// </summary>
    public class HotkeyTrigger : TriggerBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HotkeyTrigger));

        private int _registrationId = -1;
        private readonly IDictionary<string, object> _triggerParameters;
        public string HotkeyString { get; set; }
        internal IReadOnlyDictionary<string, object> TriggerParameters => _triggerParameters as IReadOnlyDictionary<string, object>;

        public HotkeyTrigger(string id, string name, string hotkeyString, string targetRecipeId, IDictionary<string, object> triggerParameters = null)
            : base(id, name, targetRecipeId)
        {
            HotkeyString = hotkeyString;
            _triggerParameters = triggerParameters == null ? null : new Dictionary<string, object>(triggerParameters);
        }

        public override void Start()
        {
            if (string.IsNullOrEmpty(HotkeyString)) return;

            Stop();
            var sequence = HotkeySequence.Parse(HotkeyString);
            if (sequence.IsEmpty)
            {
                Log.InfoFormat("Skipping hotkey registration for {0}, no hotkey set!", Name);
                return;
            }

            // An unknown key name is parsed as no key: "Ctrl + Foo" would be registered as Ctrl alone
            if (sequence.Chords.Exists(chord => chord.Key == VirtualKeyCode.None))
            {
                Log.WarnFormat("Skipping hotkey '{0}' for {1}, a key name can't be read", HotkeyString, Name);
                return;
            }

            if (!sequence.Validate(out string validationError))
            {
                Log.WarnFormat("Skipping invalid hotkey '{0}' for {1}: {2}", HotkeyString, Name, validationError);
                return;
            }

            // Never replace an already-working binding. Historically HotkeyManager replaces duplicate
            // handlers; triggers must refuse collisions so a later profile or recipe cannot steal a key.
            if (HotkeyManager.ConflictsWithRegistered(sequence))
            {
                Log.WarnFormat("Skipping hotkey '{0}' for {1}: the sequence conflicts with an existing registration.", HotkeyString, Name);
                return;
            }

            var ui = SimpleServiceProvider.Current.GetInstance<IUiDispatcher>(isOptional: true) ?? InlineUiDispatcher.Instance;

            _registrationId = HotkeyManager.RegisterHotKey(sequence, () =>
            {
                Log.DebugFormat("Hotkey '{0}' pressed for trigger '{1}' -> recipe '{2}'", HotkeyString, Name, TargetRecipeId);
                // Raise the trigger on the UI thread (posted, the keyboard hook must return quickly)
                ui.InvokeAsync(() => OnTriggered(_triggerParameters)).FireAndLog($"Hotkey trigger {Name}", Log);
            });

            if (_registrationId >= 0)
            {
                Log.DebugFormat("Registered hotkey '{0}' for trigger '{1}' (ID {2})", HotkeyString, Name, _registrationId);
            }
            else
            {
                Log.WarnFormat("Failed to register hotkey '{0}' for trigger '{1}'", HotkeyString, Name);
            }
        }

        public override void Stop()
        {
            if (_registrationId >= 0)
            {
                HotkeyManager.UnregisterHotKey(_registrationId);
                _registrationId = -1;
                Log.DebugFormat("Unregistered hotkey for trigger '{0}'", Name);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Stop();
            }
        }
    }
}
