using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace QuickPrice
{
	[BepInPlugin("zzl.moonlighter.quickprice", "QuickPrice", "0.2.0")]
	public class Plugin : BaseUnityPlugin
	{
		private sealed class TierHotkey
		{
			public ConfigEntry<string> Gamepad;

			public ConfigEntry<KeyboardShortcut> Keyboard;

			public string Tier;
		}

		private TierHotkey[] _hotkeys;

		private static readonly string[] GamepadOptions = new string[15]
		{
			"None", "A", "B", "X", "Y", "LB", "RB", "START", "BACK", "LS",
			"RS", "RS Up", "RS Down", "RS Left", "RS Right"
		};

		// Xbox-style generic joystick buttons, as exposed by Unity's legacy Input Manager
		// (KeyCode.JoystickButton0 == 330, ... JoystickButton9 == 339).
		private static readonly Dictionary<string, KeyCode> GamepadMap = new Dictionary<string, KeyCode>
		{
			{ "A", (KeyCode)330 },
			{ "B", (KeyCode)331 },
			{ "X", (KeyCode)332 },
			{ "Y", (KeyCode)333 },
			{ "LB", (KeyCode)334 },
			{ "RB", (KeyCode)335 },
			{ "BACK", (KeyCode)336 },
			{ "START", (KeyCode)337 },
			{ "LS", (KeyCode)338 },
			{ "RS", (KeyCode)339 },
		};

		// Right-stick directions mapped to Unity's generic joystick analog axes 3 (X) / 4 (Y).
		private static readonly Dictionary<string, (int Axis, float Dir)> AnalogDirMap = new Dictionary<string, (int, float)>
		{
			{ "RS Up", (4, -1f) },
			{ "RS Down", (4, 1f) },
			{ "RS Left", (3, -1f) },
			{ "RS Right", (3, 1f) },
		};

		private readonly Dictionary<string, bool> _analogWasActive = new Dictionary<string, bool>();

		private const float AxisThreshold = 0.7f;

		public static ManualLogSource LOG { get; private set; }

		internal static ConfigEntry<bool> Debug { get; private set; }

		internal static ConfigEntry<bool> EnableNotebookHighlight { get; private set; }

		internal static ConfigEntry<bool> EnableAutoPrice { get; private set; }

		internal static ConfigEntry<string> PriceTier { get; private set; }

		public void Awake()
		{
			LOG = base.Logger;

			Debug = base.Config.Bind("0. Debug", "Debug", defaultValue: false,
				new ConfigDescription("Enables debug logging.", null, new ConfigurationManagerAttributes { Order = 99 }));

			EnableNotebookHighlight = base.Config.Bind("1. Notebook", "Enable Notebook Highlight", defaultValue: true,
				new ConfigDescription("Highlights optimal prices in the notebook.", null, new ConfigurationManagerAttributes { Order = 50 }));

			EnableAutoPrice = base.Config.Bind("2. Auto Price", "Enable Auto Price", defaultValue: false,
				new ConfigDescription("Automatically sets the optimal price when placing items on the showcase.", null, new ConfigurationManagerAttributes { Order = 49 }));

			PriceTier = base.Config.Bind("2. Auto Price", "Price Tier", "MarketPrice",
				new ConfigDescription("Target price tier for auto pricing.",
					new AcceptableValueList<string>("MaxPriceInTooCheap", "MaxCorrectPrice", "MaxPriceInExpensive", "MaxPriceInTooExpensive", "MarketPrice"),
					new ConfigurationManagerAttributes { Order = 48 }));

			_hotkeys = new TierHotkey[5]
			{
				MakeTier(45, "TooCheap", "MaxPriceInTooCheap", "RS Down", new KeyboardShortcut((KeyCode)49, (KeyCode)308)),
				MakeTier(40, "Cheap", "MaxCorrectPrice", "X", new KeyboardShortcut((KeyCode)50, (KeyCode)308)),
				MakeTier(35, "Expensive", "MaxPriceInExpensive", "RS Right", new KeyboardShortcut((KeyCode)51, (KeyCode)308)),
				MakeTier(30, "TooExpensive", "MaxPriceInTooExpensive", "RS Up", new KeyboardShortcut((KeyCode)52, (KeyCode)308)),
				MakeTier(25, "Market Price", "MarketPrice", "RS Left", new KeyboardShortcut((KeyCode)53, (KeyCode)308)),
			};

			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), "zzl.moonlighter.quickprice");
			LOG.LogInfo("Plugin QuickPrice loaded.");
		}

		private TierHotkey MakeTier(int order, string label, string tier, string gpDefault, KeyboardShortcut kbDefault)
		{
			TierHotkey tierHotkey = new TierHotkey();
			tierHotkey.Gamepad = base.Config.Bind("3. Hotkeys", label + " Gamepad", gpDefault,
				new ConfigDescription("Gamepad button for " + label + ".", new AcceptableValueList<string>(GamepadOptions),
					new ConfigurationManagerAttributes { Order = order }));
			tierHotkey.Keyboard = base.Config.Bind("3. Hotkeys", label + " Keyboard", kbDefault,
				new ConfigDescription("Keyboard binding for " + label + ".", null,
					new ConfigurationManagerAttributes { Order = order - 1 }));
			tierHotkey.Tier = tier;
			return tierHotkey;
		}

		public void Update()
		{
			EventSystem current = EventSystem.current;
			if (current == null)
			{
				return;
			}

			GameObject currentSelectedGameObject = current.currentSelectedGameObject;
			if (currentSelectedGameObject == null || currentSelectedGameObject.GetComponent<ButtonPrizeHandler>() == null)
			{
				return;
			}

			ShowcaseSlotGUI componentInParent = currentSelectedGameObject.GetComponentInParent<ShowcaseSlotGUI>();
			if (componentInParent == null || componentInParent.itemStack == null)
			{
				return;
			}

			for (int i = 0; i < _hotkeys.Length; i++)
			{
				TierHotkey tierHotkey = _hotkeys[i];
				string value = tierHotkey.Gamepad.Value;

				if (value != "None" && GamepadMap.TryGetValue(value, out KeyCode gamepadKey) && Input.GetKeyDown(gamepadKey))
				{
					ApplyHotkeyPrice(componentInParent, tierHotkey.Tier);
					break;
				}

				if (value != "None" && AnalogDirMap.TryGetValue(value, out (int Axis, float Dir) analogDir) && IsAnalogDirActive(value, analogDir.Axis, analogDir.Dir))
				{
					ApplyHotkeyPrice(componentInParent, tierHotkey.Tier);
					break;
				}

				if (tierHotkey.Keyboard.Value.IsDown())
				{
					ApplyHotkeyPrice(componentInParent, tierHotkey.Tier);
					break;
				}
			}
		}

		// Edge-detects an analog stick direction crossing AxisThreshold, since generic joystick
		// axes have no built-in "just pressed" concept the way KeyCode buttons do.
		private bool IsAnalogDirActive(string name, int axisIdx, float dir)
		{
			float analogAxis = GetAnalogAxis(axisIdx);
			bool isActiveNow = (dir > 0f) ? (analogAxis > AxisThreshold) : (analogAxis < -AxisThreshold);
			bool wasActive = _analogWasActive.TryGetValue(name, out bool value) && value;
			_analogWasActive[name] = isActiveNow;
			return isActiveNow && !wasActive;
		}

		private static float GetAnalogAxis(int axisIdx)
		{
			for (int i = 1; i <= 4; i++)
			{
				float axisRaw = Input.GetAxisRaw($"joystick {i} analog {axisIdx}");
				if (axisRaw > 0.01f || axisRaw < -0.01f)
				{
					return axisRaw;
				}
			}
			return 0f;
		}

		private static void ApplyHotkeyPrice(ShowcaseSlotGUI slotGUI, string tier)
		{
			ItemPriceManager instance = ItemPriceManager.Instance;
			if (instance == null)
			{
				return;
			}

			ItemMaster master = slotGUI.itemStack.master;
			ItemStack itemStack = slotGUI.itemStack;
			bool isCool = slotGUI.ownerShowcase.itemPositions[slotGUI.slotIndex].isCool;

			int price;
			switch (tier)
			{
				case "MaxPriceInTooCheap":
					price = instance.GetMinCorrectPrice(master, isCool) - 1;
					break;
				case "MaxCorrectPrice":
					price = instance.GetMaxCorrectPrice(master, hasHighBudget: false, isCool);
					break;
				case "MaxPriceInExpensive":
					price = instance.GetMaxOverpricedPrice(master, hasHighBudget: false, isCool);
					break;
				case "MaxPriceInTooExpensive":
					price = instance.GetTooExpensiveLimitPrice(master, hasHighBudget: false, isCool);
					break;
				case "MarketPrice":
					price = instance.GetCorrectPriceWithPopularity(master, isCool);
					break;
				default:
					price = 0;
					break;
			}

			if (price > 0)
			{
				itemStack.unitSellingPrice = price;
				AccessTools.Method(typeof(ShowcaseSlotGUI), "SetLabelNumber")?.Invoke(slotGUI, new object[] { price, itemStack.Quantity });
				slotGUI.ownerShowcase.itemPositions[slotGUI.slotIndex].SetPrice(price * itemStack.Quantity);
				instance.SetLastSettedPrice(master, price);
				if (Debug.Value)
				{
					LOG.LogInfo($"Hotkey priced '{master.nameKey}' to {price} (tier: {tier}, isCool: {isCool})");
				}
			}
		}
	}
}
