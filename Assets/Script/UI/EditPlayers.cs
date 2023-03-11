using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;
using YARG.Input;
using YARG.Util;

namespace YARG.UI {
	public partial class EditPlayers : MonoBehaviour {
		[SerializeField]
		private UIDocument editPlayersDocument;

		private List<object> inputDevices;

		private int inputWaitingIndex = -1;
		private int inputWaitingPlayerIndex = -1;
		private string inputWaitingMapping = null;

		private void Start() {
			var root = editPlayersDocument.rootVisualElement;

			var radioGroup = root.Q<RadioButtonGroup>("InputStrategyRadio");
			var botMode = root.Q<Toggle>("BotMode");
			var trackSpeed = root.Q<FloatField>("TrackSpeed");
			var inputDeviceDropdown = root.Q<DropdownField>("InputDevice");
			var inputStrategyPanel = root.Q<VisualElement>("InputStrategy");
			var settingsList = root.Q<ListView>("SettingsList");
			var settingsPanel = root.Q<VisualElement>("Settings");

			inputStrategyPanel.SetOpacity(0f);
			settingsPanel.SetOpacity(0f);

			root.Q<Button>("BackButton").clicked += () => MainMenu.Instance.ShowMainMenu();

			// Initialize player list

			var playerList = root.Q<ListView>("PlayersList");

			playerList.makeItem = () => new Label();
			playerList.bindItem = (elem, i) => {
				var player = PlayerManager.players[i];

				Label item = (Label) elem;
				item.text = player.DisplayName;
			};

			playerList.itemsSource = PlayerManager.players;
			playerList.RefreshItems();

			playerList.selectedIndicesChanged += _ => {
				inputWaitingIndex = -1;
				inputWaitingPlayerIndex = -1;
				inputWaitingMapping = null;

				// Show/hide settings and input strat panel
				if (playerList.selectedIndex == -1) {
					inputStrategyPanel.SetOpacity(0f);
					settingsPanel.SetOpacity(0f);
					return;
				} else {
					inputStrategyPanel.SetOpacity(1f);
					settingsPanel.SetOpacity(1f);
				}

				var player = PlayerManager.players[playerList.selectedIndex];

				// Update input device dropdown
				var device = player.inputStrategy.inputDevice;
				var mic = player.inputStrategy.microphoneIndex;
				if (inputDevices.Contains(device)) {
					inputDeviceDropdown.index = inputDevices.IndexOf(device) + 1;
				} else if (inputDevices.Contains(mic)) {
					inputDeviceDropdown.index = inputDevices.IndexOf(mic) + 1;
				} else {
					player.inputStrategy.inputDevice = null;
					player.inputStrategy.microphoneIndex = -1;
					inputDeviceDropdown.index = -1;
				}

				// Update values
				botMode.value = player.inputStrategy.botMode;
				trackSpeed.value = player.trackSpeed;

				// Update radio group
				if (player.inputStrategy is FiveFretInputStrategy) {
					radioGroup.value = 0;
				} else if (player.inputStrategy is RealGuitarInputStrategy) {
					radioGroup.value = 1;
				} else if (player.inputStrategy is DrumsInputStrategy) {
					radioGroup.value = 2;
				} else if (player.inputStrategy is MicInputStrategy micInput) {
					radioGroup.value = 3;
				} else {
					radioGroup.value = -1;
				}

				UpdateSettingsList(settingsList, playerList.selectedIndex);
			};

			// Initialize player list buttons

			root.Q<Button>("AddPlayerButton").clicked += () => {
				PlayerManager.players.Add(new PlayerManager.Player() {
					inputStrategy = new FiveFretInputStrategy()
				});
				playerList.RefreshItems();

				// Select the new player
				playerList.selectedIndex = PlayerManager.players.Count - 1;
			};
			root.Q<Button>("RemovePlayerButton").clicked += () => {
				if (playerList.selectedIndex != -1) {
					PlayerManager.players.RemoveAt(playerList.selectedIndex);
					playerList.RefreshItems();

					// Force deselect
					playerList.selectedIndex = -1;
				}
			};

			// Initialize input strategies

			UpdateDeviceList(inputDeviceDropdown);

			inputDeviceDropdown.RegisterValueChangedCallback(e => {
				if (inputDeviceDropdown != e.target) {
					return;
				}

				if (inputDeviceDropdown.index == 0) {
					UpdateDeviceList(inputDeviceDropdown);
					inputDeviceDropdown.index = -1;
					return;
				}

				if (playerList.selectedIndex == -1 || inputDeviceDropdown.index == -1) {
					return;
				}

				var selected = inputDevices[inputDeviceDropdown.index - 1];

				var inputStrat = PlayerManager.players[playerList.selectedIndex].inputStrategy;
				if (selected is InputDevice inputDevice) {
					inputStrat.inputDevice = inputDevice;
					inputStrat.microphoneIndex = -1;
				} else if (selected is int mic) {
					inputStrat.inputDevice = null;
					inputStrat.microphoneIndex = mic;
				}
			});

			botMode.RegisterValueChangedCallback(e => {
				if (botMode != e.target) {
					return;
				}

				if (playerList.selectedIndex == -1) {
					return;
				}

				var player = PlayerManager.players[playerList.selectedIndex];

				player.inputStrategy.botMode = e.newValue;
				playerList.RefreshItem(playerList.selectedIndex);
			});

			trackSpeed.RegisterValueChangedCallback(e => {
				if (trackSpeed != e.target) {
					return;
				}

				if (playerList.selectedIndex == -1) {
					return;
				}

				var player = PlayerManager.players[playerList.selectedIndex];
				player.trackSpeed = trackSpeed.value;
			});

			radioGroup.RegisterValueChangedCallback(e => {
				if (radioGroup != e.target) {
					return;
				}

				if (playerList.selectedIndex == -1) {
					return;
				}

				var player = PlayerManager.players[playerList.selectedIndex];
				var oldInput = player.inputStrategy?.inputDevice;
				var oldMic = player.inputStrategy?.microphoneIndex ?? -1;
				switch (e.newValue) {
					case 0:
						player.inputStrategy = new FiveFretInputStrategy {
							inputDevice = oldInput,
							microphoneIndex = oldMic,
							botMode = botMode.value
						};
						break;
					case 1:
						player.inputStrategy = new RealGuitarInputStrategy {
							inputDevice = oldInput,
							microphoneIndex = oldMic,
							botMode = botMode.value
						};
						break;
					case 2:
						player.inputStrategy = new DrumsInputStrategy {
							inputDevice = oldInput,
							microphoneIndex = oldMic,
							botMode = botMode.value
						};
						break;
					case 3:
						player.inputStrategy = new MicInputStrategy {
							inputDevice = oldInput,
							microphoneIndex = oldMic,
							botMode = botMode.value
						};
						break;
				}

				UpdateSettingsList(settingsList, playerList.selectedIndex);
			});
		}

		private void UpdateDeviceList(DropdownField dropdownField) {
			var choices = new List<string>() {
				"Refresh..."
			};

			inputDevices = new();

			// Add controllers
			foreach (var device in InputSystem.devices) {
				inputDevices.Add(device);
				choices.Add(device.name);
			}

			// Add microphones
			for (int i = 0; i < Microphone.devices.Length; i++) {
				inputDevices.Add(i);
				choices.Add(Microphone.devices[i]);
			}

			dropdownField.choices = choices;
		}

		private void UpdateSettingsList(ListView settingsList, int playerId) {
			var player = PlayerManager.players[playerId];

			settingsList.itemsSource = player.inputStrategy.GetMappingNames();

			settingsList.makeItem = () => new Button();
			settingsList.bindItem = (elem, i) => {
				var mapping = player.inputStrategy.GetMappingNames()[i];
				var inputDisplayName = player.inputStrategy
					.GetMappingInputControl(mapping)?.displayName
					?? "None";

				Button button = (Button) elem;
				button.text = $"{mapping}: {inputDisplayName}";

				// Remove old events
				button.clickable = null;

				// Add new events
				button.clicked += () => {
					settingsList.RefreshItem(inputWaitingIndex);

					inputWaitingIndex = i;
					inputWaitingPlayerIndex = playerId;
					inputWaitingMapping = mapping;

					button.text = "Waiting for input...";
				};
			};

			settingsList.Rebuild();
		}

		private void Update() {
			if (inputWaitingIndex == -1) {
				return;
			}

			var player = PlayerManager.players[inputWaitingPlayerIndex];
			var device = player.inputStrategy.inputDevice;

			if (device == null) {
				return;
			}

			foreach (var control in device.allControls) {
				// Skip "any key" (as that would always be detected)
				if (control is AnyKeyControl) {
					continue;
				}

				if (control is not ButtonControl buttonControl) {
					continue;
				}

				if (!buttonControl.wasPressedThisFrame) {
					continue;
				}

				// Set mapping and stop waiting
				player.inputStrategy.SetMappingInputControl(inputWaitingMapping, control);
				editPlayersDocument.rootVisualElement.Q<ListView>("SettingsList").RefreshItem(inputWaitingIndex);
				inputWaitingIndex = -1;
				inputWaitingPlayerIndex = -1;
				inputWaitingMapping = null;
				break;
			}
		}
	}
}