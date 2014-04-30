behavior("/", {
    open: function(evt) {
        var connid = evt.connection.id;
        var name = evt.token;

        evt.domain.findall("user:*", function(err, list) {
            var users;
            var expr;
            
            if (err) {
                evt.deny(err);
            }
            
            var users = list.map(function (expr) {
                var splitted = expr.split(":");
                return { id: splitted[0], name: splitted[1] };
            });

            if (name == "[OBSERVER]") {
                evt.allow(JSON.stringify({ users: users }));
            }

            expr = connid + ":" + name;

            evt.domain.set("user:" + connid, expr, function(err) {
                if (err) {
                    evt.deny(err);
                }
                
                evt.channel.emit(JSON.stringify({
                    op: "user-online",
                    id: connid,
                    name: name
                }));
            
                evt.allow(JSON.stringify({
                    id: connid,
                    users: users
                }));
            });
        });
    },
    
    close: function(evt) {
        var connid = evt.connection.id;
        evt.domain.del("user:" + connid);
        evt.channel.emit(JSON.stringify({
            op: "user-offline",
            id: connid
        }));
    }
});


behavior("/user/{id}", {
    open: function(evt) {
        console.log("Connected " + evt.params.id);
        if (evt.write) {
            if (evt.connection.id != evt.params.id) {
                evt.deny("Only owner is allowed to open channel in write-mode");
            }
        }
    },
    
    emit: function(evt) {
        evt.channel.emit(evt.data);
    },
    
    close: function(evt) {
        if (evt.channel.id == evt.params.id) {
            evt.channel.emit(JSON.stringify({ op: "EOT" }));
        }
    }
});