using System;
using BepInEx.Configuration;

internal sealed class ConfigurationManagerAttributes
{
	public delegate void CustomHotkeyDrawerFunc(ConfigEntryBase setting, KeyboardShortcut key);

	public int? Order;

	public bool? Browsable;

	public Action<ConfigEntryBase> CustomDrawer;

	public CustomHotkeyDrawerFunc CustomHotkeyDrawer;
}
