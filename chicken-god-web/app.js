(function() {
"use strict";

var requestAnimationFrame = (function(){
  return  window.requestAnimationFrame       ||
          window.webkitRequestAnimationFrame ||
          window.mozRequestAnimationFrame    ||
          function(callback){
            window.setTimeout(callback, 1000 / 60);
          };
})();

var MAP_WIDTH = 500;
var MAP_HEIGHT = 500;
var MAP_ORIGINX = MAP_WIDTH / 2;
var MAP_ORIGINY = MAP_HEIGHT / 2;

var HY_DOMAIN = "hy-chickenrace.hydna.net/";
var OP_MOVEMENT = 0x1;

var players = [];


function Player(id, name) {
  this.id = id;
  this.name = name;
  this.url = HY_DOMAIN + "user/" + id;
  this.channel = new HydnaChannel(this.url, "re");
  this.bounds = [55, 55];
  this.srvpos = [0, 0, 0];
  this.pos = [0, 0, 0];
  this.angle = 0;
  this.lastUpdate = 0;
  this.state = 0;
  this.sprite = document.createElement("canvas");
  this.spriteCtx = this.sprite.getContext("2d");
  this.nameSprite = document.createElement("canvas");
  this.selected = false;
}


(function mapController() {
  var map;
  var grantFox;
  var grantMegaChicken;
  var revoke;
  var ctx;
  var coords;

  map = document.querySelector("#map canvas");
  ctx = map.getContext("2d");
  grantFox = document.querySelector("#map .grant-fox");
  grantMegaChicken = document.querySelector("#map .grant-megachicken");
  revoke = document.querySelector("#map .revoke");

  map.width = MAP_WIDTH;
  map.height = MAP_HEIGHT;

  coords = [0, 0];

  (function updateLoop() {
    var player;

    ctx.clearRect(0, 0, MAP_WIDTH, MAP_HEIGHT);

    ctx.save();

    ctx.translate(MAP_ORIGINX, MAP_ORIGINY);

    for (var idx = 0; idx < players.length; idx++) {
      player = players[idx];

      interpolatePos(player.pos, player.srvpos);
      posToScreenCoords(player.pos, coords);

      ctx.save();
      ctx.translate(coords[0], coords[1]);
      ctx.rotate(getAngleFromPos(player.pos, player.srvpos) * Math.PI / 180);
      ctx.translate(-(player.sprite.width / 2), -(player.sprite.height / 2));
      ctx.drawImage(player.sprite, 0, 0);

      ctx.fillStyle = "white";
      ctx.fillRect((player.sprite.width / 2),
                   -player.sprite.height,
                   .5,
                   player.sprite.height + 4);

      ctx.restore();

      if (player.selected) {
        ctx.save();
        ctx.translate(coords[0], coords[1]);
        ctx.lineWidth = .4;
        ctx.strokeStyle = "rgba(255, 127, 0, 1)";
        ctx.strokeRect(-20, -20, 40, 40);
        ctx.restore();
      }

      ctx.save();
      ctx.translate(coords[0], coords[1]);
      ctx.fillStyle = "#444";
      ctx.textAlign = "center";
      ctx.fillText(player.name, 0, player.sprite.height * 1.8);
      ctx.restore();

    }

    ctx.restore();

    requestAnimationFrame(updateLoop);
  }());

  map.addEventListener("click", function(evt) {
    var player;

    evt.preventDefault();

    player = getPlayerByCoords(mouseToScreenCoords(evt.offsetX, evt.offsetY));

    selectPlayer(player);
  });


  function buttonAction(evt) {
    var player;
    var type;

    evt.preventDefault();

    type = this.getAttribute("data-type");

    if (player = getSelectedPlayer()) {
      setPlayerSprite(player, type);
      player.channel.emit(JSON.stringify({ op: "grant", type: type }));
    }
  }

  revoke.addEventListener("click", buttonAction);
  grantFox.addEventListener("click", buttonAction);
  grantMegaChicken.addEventListener("click", buttonAction);


}());


(function lobbyController() {
  var channel;
  var textarea;

  players = [];

  setStatus("Connecting...");

  channel = new HydnaChannel("hy-chickenrace.hydna.net/?[OBSERVER]", "rw");

  channel.onopen = function(evt) {
    var packet = JSON.parse(evt.data);
    packet.users.forEach(addPlayer);
    updatePlayerCount();
    appendNotice("Connected to server");
  };

  channel.onsignal = function(evt) {
    var packet = JSON.parse(evt.data);
    var player;

    switch (packet.op) {

    case "user-online":
      if ((player = addPlayer(packet))) {
        appendNotice("Player '" + player.name + "' joined the game...");
        updatePlayerCount();
      }
      break;

    case "user-offline":
      if ((player = removePlayer(packet))) {
        appendNotice("Player '" + player.name + "' disconnected...");
        updatePlayerCount();
      }
      break;
    }
  };

  channel.onmessage = function(evt) {
    var msg;

    msg = JSON.parse(evt.data);

    appendMessage(msg);
  };

  channel.onclose = function() {
    setStatus("Disconnected");
    players.forEach(removePlayer);
    setTimeout(lobbyController, 2000);
  };

  textarea = document.querySelector("#chatbox textarea");
  textarea.addEventListener("keyup", function (evt) {
    var code = evt.keyCode || evt.which;
    if (code == 13) {
      channel.send(JSON.stringify({
        from: "The Chicken GOD",
        timestamp: Date.now(),
        body: textarea.value
      }));
      textarea.value = "";
    }
  });
}());


function addPlayer(data) {
  var player;
  var channel;

  player = new Player(data.id, data.name);

  setPlayerSprite(player, "chicken");

  player.channel.onopen = function(evt) {};

  player.channel.onmessage = function(evt) {
    switch (readOp(evt.data)) {

    case OP_MOVEMENT:
      updatePlayerMovement(player, new DataView(evt.data, 1));
      break;

    }
  };

  players.push(player);

  return player;
}


function removePlayer(data) {
  var player;
  for (var idx = 0; idx < players.length; idx++) {
    player = players[idx];
    if (player.id == data.id) {
      player.channel.close();
      players.splice(idx, 1);
      return player;
    }
  }
}


function readOp(data) {
  return (new Uint8Array(data, 0, 1))[0];
}


function updatePlayerMovement(player, data) {
  player.lastUpdate = data.getFloat32(0, true);
  player.srvpos[0] = data.getFloat32(4, true);
  player.srvpos[1] = data.getFloat32(8, true);
  player.srvpos[2] = data.getFloat32(12, true);
  player.angle = data.getFloat32(16, true) - 180;
  player.state = data.getFloat32(20, true);
}


function mouseToScreenCoords(x, y) {
  return [x - MAP_ORIGINX, y - MAP_ORIGINY];
}


function posToScreenCoords(pos, coords) {
  coords[0] = (pos[0] / 5) * MAP_ORIGINX;
  coords[1] = (pos[2] / 5) * MAP_ORIGINY;
}


function getPlayerByCoords(coords) {
  var player;
  var bounds;
  var pcoords;

  pcoords = [0, 0];

  for (var idx = 0; idx < players.length; idx++) {
    player = players[idx];
    bounds = player.bounds;

    posToScreenCoords(player.pos, pcoords);

    if (coords[0] >= pcoords[0] - bounds[0] &&
        coords[0] <= pcoords[0] + bounds[0] &&
        coords[1] >= pcoords[1] - bounds[1] &&
        coords[1] <= pcoords[1] + bounds[1]) {
      return player;
    }
  }
}


function interpolatePos(pos1, pos2) {
  pos1[0] = interpolate(pos1[0], pos2[0], 0.2);
  pos1[2] = interpolate(pos1[2], pos2[2], 0.2);

}


function interpolate(y1, y2, mu) {
   var mu2 = (1 - Math.cos(mu * Math.PI)) / 2;
   return(y1 * (1 - mu2) + y2 * mu2);
}


function getAngleFromPos(pos1, pos2) {
  var a = Math.atan2(pos1[2] - pos2[2],  pos1[0] - pos2[0]) * 180 / Math.PI;
  return a - 90;
}


function setStatus(txt) {
  var status = document.getElementById("status");
  status.innerHTML = txt;
}


function updatePlayerCount() {
  setStatus("Connected (" + players.length  + " users online)");
}


function selectPlayer(player) {
  for (var idx = 0; idx < players.length; idx++) {
    players[idx].selected = false;
  }

  if (player) {
    player.selected = true;
  }
}


function getSelectedPlayer() {
  for (var idx = 0; idx < players.length; idx++) {
    if (players[idx].selected) {
      return players[idx];
    }
  }
}


function setPlayerSprite(player, type) {
  switch (type) {

  case "chicken":
    player.sprite.width = 10;
    player.sprite.height = 10;
    player.spriteCtx.fillStyle = "blue";
    player.spriteCtx.fillRect(0, 0, 10, 10);
    break;

  case "fox":
    player.sprite.width = 10;
    player.sprite.height = 10;
    player.spriteCtx.fillStyle = "red";
    player.spriteCtx.fillRect(0, 0, 10, 10);
    break;

  case "megachicken":
    player.sprite.width = 20;
    player.sprite.height = 20;
    player.spriteCtx.fillStyle = "blue";
    player.spriteCtx.fillRect(0, 0, 20, 20);
    break;
  }
}


function appendNotice(text) {
  var list;
  var li;
  var d;
  var hour;
  var min;

  list = document.querySelector("#chatbox ul");
  li = document.createElement("li");
  li.className = "notice";

  d = new Date();
  hour = padtime(d.getHours());
  min = padtime(d.getMinutes());

  li.innerHTML = "** (" + hour + ":" + min + ") " + text;

  list.appendChild(li);
  li.scrollIntoView(true);
}


function appendMessage(msg) {
  var list;
  var li;
  var d;
  var hour;
  var min;

  list = document.querySelector("#chatbox ul");
  li = document.createElement("li");

  d = new Date(msg.timestamp);
  hour = padtime(d.getHours());
  min = padtime(d.getMinutes());

  li.innerHTML = '<span class="date">' + hour + ':' + min + '</span>' +
                 '<span class="from">' + msg.from + '</span>' +
                 '<p>' + msg.body + '</p>';

  list.appendChild(li);
  li.scrollIntoView(true);
}


function padtime(no) {
  return no < 10 ? "0" + no : no;
}


}());
