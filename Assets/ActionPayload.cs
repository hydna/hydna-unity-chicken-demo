using UnityEngine;
using System.IO;

class ActionPayload {
	
	public const byte OP_MOVEMENT = 1;
	public const byte OP_ACTION = 2;
	
	public byte op;
	public float time;
	public Vector3 position;
	public float rot;
	
	public int state;
	
	public static ActionPayload Decode(byte[] payload) {
		
		MemoryStream stream = new MemoryStream(payload);
		BinaryReader reader = new BinaryReader(stream);
		
		byte op = reader.ReadByte ();
		
		ActionPayload action = new ActionPayload();
		
		switch (op) {
			
			case ActionPayload.OP_MOVEMENT:
			
				action.op = op;
				action.time = reader.ReadSingle ();
				
				action.position = new Vector3();
				action.position.x = reader.ReadSingle ();
				action.position.y = reader.ReadSingle ();
				action.position.z = reader.ReadSingle ();
				
				action.rot = reader.ReadSingle ();
				action.state = (int)reader.ReadSingle ();
				
			break;
		}
		
		return action;
	}
}