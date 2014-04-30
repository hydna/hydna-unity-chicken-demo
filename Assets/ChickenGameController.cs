using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Hydna.Net;
using SimpleJSON;

public class ChickenGameController : MonoBehaviour {

	// Constants
	private const string HYDNA_DOMAIN = "hy-chickenrace.hydna.net";
	private const string USER_ONLINE = "user-online";
	private const string USER_OFFLINE = "user-offline";
	private const int MAX_TICK = 5;

	// Private
	private Channel lobby_conn;
	private Channel player_conn;
	private string player_id;
	private string player_name;
	private ThirdPersonController player_controller;
	private ThirdPersonCamera camera_controller;

	private Vector3 player_last_position;
	private float player_last_rotation;
	private int player_last_state;
	private string player_skin = ChickenPlayer.CHICKEN_SKIN;
	private bool player_switch_skin = false;
	private bool player_stale_position = true;

	private Dictionary<string, ChickenPlayer> players;
	private int tick = 0;

	private bool name_entered = false;
	private string name_input_txt = "";
	private string chat_input_txt = "";
	private string chat_buffer = "";
	private Vector2 scrollPosition;
	private bool show_chat = false;
	private bool hasSentEnter = false;

	private GUIStyle TextInputStyle;

	// Public
	public GameObject Player;
	public GameObject ChickenClone;
	public GameObject FoxClone;

	public GUIStyle PlayerLabelStyle;
	public GUIStyle OpponentLabelStyle;

	void Awake() {
		if (Player) {
			Player.transform.position = new Vector3(Random.Range(-4.8f, 4.8f), Player.transform.position.y, Random.Range(-4.8f, 1.5f));
		}
	}

	void Start() {

		players = new Dictionary<string, ChickenPlayer>();

		if (Player) {

			player_controller = Player.GetComponent<ThirdPersonController>();
			camera_controller = Player.GetComponent<ThirdPersonCamera>();

			player_last_position = Player.transform.position;
			player_last_rotation = Player.transform.eulerAngles.y;
			player_last_state = player_controller.GetState();
		}
	}


	void ConnectLobby() {

		lobby_conn = new Channel();

		lobby_conn.Connect (HYDNA_DOMAIN + "?" + player_name, ChannelMode.ReadWrite);
		lobby_conn.Open += delegate(object sender, ChannelEventArgs e) {

			var info = JSON.Parse(e.Text);

			string userId = info["id"].Value;

			JSONArray users = info["users"].AsArray;

			player_id = userId;

			ConnectMe(player_id);

			if (users.Count > 0) {

				for (int i = 0; i < users.Count; i++) {
					AddChicken(users[i]["id"].Value, users[i]["name"].Value);
				}
			}

			chat_buffer += "* Connected to lobby";
		};

		lobby_conn.Data += delegate(object sender, ChannelDataEventArgs e) {

			var info = JSON.Parse(e.Text);

			string from = info["from"].Value;
			string timestamp = info["timestamp"].Value;
			string body = info["body"].Value;

			chat_buffer += "\n" + "* " + from + ": " + body;

			scrollPosition.y = Mathf.Infinity;

		};

		lobby_conn.Signal += delegate(object sender, ChannelEventArgs e) {

			if (lobby_conn.State != ChannelState.Open) {
				return;
			}

			var info = JSON.Parse(e.Text);

			string playerId;
			string name;

			switch (info["op"]) {
				case USER_ONLINE:

					playerId = info["id"].Value;
					name = info["name"].Value;

					chat_buffer += "\n* " + name + " joined the demo";

					AddChicken(playerId, name);

				break;

				case USER_OFFLINE:

					playerId = info["id"].Value;
					name = players[playerId].Name;

					chat_buffer += "\n" + "* " + name + " left the demo";

					RemoveChicken(playerId, name);

				break;
			}
		};

		lobby_conn.Closed += delegate(object sender, ChannelCloseEventArgs e) {
			chat_buffer += "\n* Disconnected from demo";
		};
	}

	bool HasMoved() {

		if (Vector3.Distance(player_last_position, Player.transform.position) > 0.1f) {
			return true;
		}

		if (player_last_rotation != Player.transform.eulerAngles.y) {
			return true;
		}

		if (player_last_state != player_controller.GetState()) {
			return true;
		}

		return false;
	}

	void SendState() {

		if (player_conn.State == ChannelState.Open) {

			// check if position or state has changed...
			if (HasMoved() || player_stale_position) {

				player_last_position = Player.transform.position;
				player_last_rotation = Player.transform.eulerAngles.y;
				player_last_state = player_controller.GetState();

				MemoryStream stream = new MemoryStream ();
				BinaryWriter writer = new BinaryWriter (stream);

				writer.Write (ActionPayload.OP_MOVEMENT);
				writer.Write (Time.fixedTime);
				writer.Write (player_last_position.x);
				writer.Write (player_last_position.y);
				writer.Write (player_last_position.z);
				writer.Write (player_last_rotation);
				writer.Write ((float)player_last_state);

				player_conn.Send (stream.ToArray());

				player_stale_position = false;
			}
		}
	}


	void AddChicken(string playerId, string name) {

		lock (players) {
			if (!players.ContainsKey (playerId) && playerId != player_id) {

				ChickenPlayer chicklet = new ChickenPlayer(playerId, name);

				chicklet.Connect(HYDNA_DOMAIN + "/user/" + playerId);

				players.Add(playerId, chicklet);

				tick = MAX_TICK;
				player_stale_position = true;
			}
		}

		if (players.Count > 0) {
			camera_controller.distance = 4.0f;
		}

	}

	void RemoveChicken(string playerId, string name) {

		lock (players) {
			if (players.ContainsKey (playerId)) {
				players[playerId].Execute();
			}
		}

		if (players.Count < 2) {
			camera_controller.distance = 2.0f;
		}
	}


	void ConnectMe(string id) {

		player_conn = new Channel();

		player_conn.Connect (HYDNA_DOMAIN + "/user/" + id, ChannelMode.Write);
		player_conn.Open += delegate(object sender, ChannelEventArgs e) {
			Debug.Log("player channel connected");
		};

		player_conn.Signal += delegate(object sender, ChannelEventArgs e) {

			var info = JSON.Parse(e.Text);

			string op = info["op"].Value;
			string type = info["type"].Value;

			if (op == ChickenPlayer.OP_GRANT) {

				if (type != player_skin) {
					player_skin = type;
					player_switch_skin = true;
				}
			}

			if (op == ChickenPlayer.OP_PING) {
				player_stale_position = true;
				tick = MAX_TICK;
			}

		};

		player_conn.Closed += delegate(object sender, ChannelCloseEventArgs e) {
			Debug.Log("player channel closed: " + e.Reason);
		};

	}

	void OnGUI() {

		TextInputStyle = GUI.skin.textField;
		TextInputStyle.fontSize = 14;
		TextInputStyle.alignment = TextAnchor.MiddleLeft;

		if (name_entered) {

			// Draw character labels
			Vector3 offset = new Vector3(0.0f, -0.5f, 0.0f);
			Vector3 point = Camera.main.WorldToScreenPoint(Player.transform.position - offset);
			Rect rect = new Rect(0,0,300,20);
			rect.x = point.x - (rect.width * 0.5f);
			rect.y = Screen.height - (point.y + rect.height);
			GUI.Label(rect, player_name, PlayerLabelStyle);

			lock (players) {
				foreach (string key in players.Keys) {

					ChickenPlayer chicken = players [key];
					GameObject body = chicken.getGameObject();

					if (body && chicken.HasMoved) {
						point = Camera.main.WorldToScreenPoint(chicken.getGameObject().transform.position - offset);
						rect.x = point.x - (rect.width * 0.5f);
						rect.y = Screen.height - (point.y + rect.height);
						GUI.Label(rect, chicken.Name, OpponentLabelStyle);
					}
				}
			}

			// Draw chat
			if (show_chat) {

				GUI.Box (new Rect (20.0f, 20.0f, Screen.width - 40.0f, Screen.height - 100.0f), "Let's have a chat");

				GUILayout.BeginArea(new Rect(40.0f, 40.0f + 20.0f, Screen.width - 80.0f, Screen.height - 160.0f));
				scrollPosition = GUILayout.BeginScrollView(scrollPosition);

				GUILayout.Label (chat_buffer);

				GUILayout.EndScrollView();
				GUILayout.EndArea();

				Event e = Event.current;
				if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return) {
					if (!hasSentEnter) {
						SendChat();
						hasSentEnter = true;
					}
				}

				if (e.type == EventType.KeyUp && e.keyCode == KeyCode.Return) {
					hasSentEnter = false;
				}

				chat_input_txt = GUI.TextField (new Rect (20.0f, Screen.height - 60.0f, Screen.width - 140.0f, 40.0f), chat_input_txt, 200);

				if (GUI.Button (new Rect (Screen.width - 100.0f, Screen.height - 60.0f, 80.0f, 40.0f), "Send")) {

					SendChat();
				}

				if (GUI.Button (new Rect (Screen.width - 60.0f, 10.0f, 50.0f, 50.0f), "x")) {

					ToggleChat();
				}

			} else {

				if(GUI.Button (new Rect (20.0f, 20.0f, 60.0f, 40.0f), "Chat")) {

					ToggleChat();
				}
			}

		} else {

			// Draw name input
			float startx = (Screen.width * 0.5f) - 155.0f;
			float starty = (Screen.height * 0.5f) - 40.0f;

			Rect rect = new Rect(startx, starty - 25, 100, 20);
			GUI.Label(rect, "Pick a name:");


			Event e = Event.current;
			if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return) {
				if (!hasSentEnter) {
					PickName();
					hasSentEnter = true;
				}
			}

			if (e.type == EventType.KeyUp && e.keyCode == KeyCode.Return) {
				hasSentEnter = false;
			}

			name_input_txt = GUI.TextField (new Rect (startx, starty, 200.0f, 40.0f), name_input_txt, 200, TextInputStyle);

			if (GUI.Button (new Rect (startx + 210.0f, starty, 100.0f, 40.0f), "Go")) {

				PickName();
			}

		}
	}

	void PickName() {

		if (name_input_txt.Length > 0) {

			player_name = name_input_txt;
			name_entered = true;

			ConnectLobby();
		}
	}

	void SendChat() {

		if (lobby_conn.State == ChannelState.Open && chat_input_txt.Length > 0) {

			string payload = "{\"from\":\"" + player_name + "\", \"timestamp\":0, \"body\":\"" + chat_input_txt + "\"}";

			lobby_conn.Send(payload);
		}

		chat_input_txt = "";

	}

	void ToggleChat() {

		if (show_chat) {
			show_chat = false;
		} else {
			show_chat = true;
			scrollPosition.y = Mathf.Infinity;
		}
	}

	void Update() {

		if (player_conn != null && player_conn.State == ChannelState.Open) {
			if (tick >= MAX_TICK) {

				SendState();

				tick = 0;
			}
		}

		if (player_switch_skin) {
			player_controller.SwitchSkin(player_skin);
		}

		string removeKey = null;

		lock (players) {
			foreach (string key in players.Keys) {

				ChickenPlayer chicken = players[key];

				GameObject cloning = FoxClone;

				if (chicken.getSkin() != ChickenPlayer.FOX_SKIN) {
					cloning = ChickenClone;
				}

				if (!chicken.HasInitiated) {
					GameObject clone = Instantiate(cloning) as GameObject;
					chicken.Init (clone);
				}

				chicken.Update();

				if (chicken.ShouldSwitch) {

					Destroy(chicken.getGameObject());
					GameObject clone = Instantiate(cloning) as GameObject;
					chicken.SwitchSkin (clone);
					chicken.Deflate();

					if (chicken.getSkin() == ChickenPlayer.MEGACHICKEN_SKIN) {
						chicken.Inflate(3.5f);
					} else {
						chicken.Deflate();
					}
				}

				if (chicken.ShouldRemove) {
					if (removeKey == null) {
						chicken.Die();
						Destroy(chicken.getGameObject(), 2.0f);

						removeKey = key;
					}
				}
			}

			if (removeKey != null) {
				players.Remove(removeKey);
			}
		}

		tick += 1;
	}
}
