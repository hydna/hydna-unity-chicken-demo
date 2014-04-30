using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Hydna.Net;
using SimpleJSON;

class ChickenPlayer {

	public const string OP_GRANT = "grant";
	public const string OP_PING = "ping";

	public const string FOX_SKIN = "fox";
	public const string MEGACHICKEN_SKIN = "megachicken";
	public const string CHICKEN_SKIN = "chicken";
	public const string PING = "ping";
	
	public string Id;
	public string Name;
	
	public bool HasInitiated = false;
	public bool HasMoved = false;
	public bool ShouldRemove = false;
	public bool ShouldSwitch = false;
	
	private GameObject player;
	private Animation animations;
	private Channel conn;
	private List<ActionPayload> updates;
	
	private Vector3 target_position;
	private float target_rotation;
	private float target_scale = 1.0f;
	private int animationState = 0;
	
	private string skin = CHICKEN_SKIN;
	
	private Dictionary<int, string> stateAnimation = new Dictionary<int, string>()
	{
		{ 0, "idleEat"},
		{ 1, "walk"},
		{ 2, "run"},
		{ 3, "runAngry"},
		{ 4, "death"}
	};
	
	public ChickenPlayer(string id, string name) {
		
		Id = id;
		Name = name;
		
		updates = new List<ActionPayload>();
		conn = new Channel ();
	}
	
	public void Init(GameObject target) {
		if (!HasInitiated) {
			HasInitiated = true;
			player = target;
			
			animations = player.GetComponent<Animation>();
			animations.CrossFade(stateAnimation[animationState]);

			player.SetActive(false);
		}
	}
	
	public void Connect(string uri) {

		conn.Connect (uri, ChannelMode.ReadEmit);
		conn.Open += delegate(object sender, ChannelEventArgs e) {

			string payload = "{\"op\":\"" + OP_PING + "\",type:\"" + PING + "\"}";
			conn.Emit(payload);
		};
		
		conn.Data += delegate(object sender, ChannelDataEventArgs e) {
			
			// add to message que
			updates.Add(ActionPayload.Decode(e.Payload));
			
		};
		
		conn.Signal += delegate(object sender, ChannelEventArgs e) {

			var info = JSON.Parse(e.Text);
			
			string op = info["op"].Value;
			string type = info["type"].Value;
			
			if (op == OP_GRANT) {

				if (type != skin) {
					skin = type;
					ShouldSwitch = true;
				}
			}
		};
		
		conn.Closed += delegate(object sender, ChannelCloseEventArgs e) {
			Debug.Log(" closed channel: " + Id + ", reason: " + e.Reason);
		};	
	}
	
	
	public void Update() {
		
		if (updates.Count > 0 && player != null) {
			
			ActionPayload action = updates[0];
			
			switch(action.op) {
				case ActionPayload.OP_MOVEMENT:
				
					target_position = action.position;
					target_rotation = action.rot;
					
					if (action.state != animationState) {
						
						ChangeState (action.state);
					}

					if (!HasMoved) {
						player.transform.position = target_position;
						player.transform.eulerAngles = new Vector3(player.transform.eulerAngles.x, target_rotation, player.transform.eulerAngles.z);
						player.SetActive(true);
						animations.CrossFade(stateAnimation[animationState]);
						HasMoved = true;
					}
				
				break;
			};
			
			updates.RemoveAt(0);
		}
		
		player.transform.position = Vector3.Lerp(player.transform.position, target_position, 0.1f);
		
		float angle = Mathf.LerpAngle(player.transform.eulerAngles.y, target_rotation, 0.1f);
		player.transform.eulerAngles = new Vector3(player.transform.eulerAngles.x, angle, player.transform.eulerAngles.z);
		
		float scale = Mathf.Lerp(player.transform.localScale.x, target_scale, 0.2f);
		player.transform.localScale = new Vector3 (scale, scale, scale);
		
	}
	
	public void Execute() {
		ShouldRemove = true;
		conn.Close ();
	}
	
	public void Die() {
		animationState = 4;
		animations [stateAnimation [animationState]].wrapMode = WrapMode.Once;
		animations.CrossFade(stateAnimation[animationState]);
	}
	
	public void SwitchSkin(GameObject skin) {
		
		Vector3 current_pos = player.transform.position;
		Vector3 current_rot = player.transform.eulerAngles;
		Vector3 current_scale = player.transform.localScale;
		
		player = skin;
		player.transform.position = current_pos;
		player.transform.eulerAngles = current_rot;
		player.transform.localScale = current_scale;
		
		animations = player.GetComponent<Animation>();
		animations.CrossFade(stateAnimation[animationState]);
		
		ShouldSwitch = false;
	}
	
	public string getSkin() {
		return skin;
	}
	
	public GameObject getGameObject() {
		return player;
	}
	
	public void Inflate(float target) {
		target_scale = target;
	}
	
	public void Deflate() {
		target_scale = 1.0f;
	}
	
	public float getScale() {
		return target_scale;
	}
	
	public void ChangeState(int state) {
		animationState = state;
		animations.CrossFade(stateAnimation[animationState]);
	}
}