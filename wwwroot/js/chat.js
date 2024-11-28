"use strict";

let connection = new signalR.HubConnectionBuilder().withUrl("/GameHub").build();

const canvas = document.getElementById("canvas");
const ctx = canvas.getContext("2d");

let blue_team_players = [];
let red_team_players = [];

//Disable the send button until connection is established.
document.getElementById("sendButton").disabled = true;
document.getElementById("actualButton").disabled = true;

connection.start().then(function () {
    document.getElementById("sendButton").disabled = false;
    document.getElementById("actualButton").disabled = false;
    connection.invoke("Connect", connection.connectionId).catch(function (err) {
        return console.error(err.toString());
    });
})

document.getElementById("sendButton").addEventListener("click", function (event) {
    let command = document.getElementById("messageInput").value.split(/[.,()]+/);
    // let command = message.split(/[.()]+/)
    console.log(command);
    switch (command[0]) {
        case "move":
            console.log("move: " + command[1]);
            connection.invoke("MoveTask", connection.connectionId.toString(), Number(command[1]), Number(command[2]))
            .catch(function (err) { 
                return console.error(err.toString());
            });
            break;
        case "game":
            if(command[1] === "start") {
                connection.invoke("StartGame").catch(function (err) {
                    return console.error(err.toString());
                });
            } else if(command[1] === "stop") {
                console.log("stop");
                connection.invoke("StopGame").catch(function (err) {
                    return console.error(err.toString());
                });
            }
            break;
        case "character":
            if(command[1] === "select") {
                if(command[2] === "Sukuna" || command[2] === "sukuna") {
                    connection.invoke("SelectCharacter", connection.connectionId.toString(), "sukuna").catch(function (err) {
                        return console.error(err.toString());
                    });
                } else if(command[2] === "Momo" || command[2] === "momo") {
                    connection.invoke("SelectCharacter", connection.connectionId.toString(), "momo").catch(function (err) {
                        return console.error(err.toString());
                    });
                }}
        case "click":
            connection.invoke("ClickTask", connection.connectionId.toString(), Number(command[1]), Number(command[2]))
        case "skill":
            console.log("skill: " + command[1]);
            if(command[1] === "q")
                connection.invoke("SkillQ", connection.connectionId.toString())
            if(command[1] === "w")
                connection.invoke("SkillW", connection.connectionId.toString())
            if(command[1] === "e")
                connection.invoke("SkillE", connection.connectionId.toString())
            if(command[1] === "r")
                connection.invoke("SkillR", connection.connectionId.toString())
            .catch(function (err) { 
                return console.error(err.toString());
            });
            break;
    }
});

document.getElementById("actualButton").addEventListener("click", function (event) {
    console.log("actual");
    connection.invoke("GetActualBlueTeam").catch(function (err) {
        return console.error(err.toString());
    });
    connection.invoke("GetActualRedTeam").catch(function (err) {
        return console.error(err.toString());
    });
});

connection.on("ActualBlueTeam", function (blue_team) {
    console.log(blue_team);
    blue_team_players = blue_team;
    let blue_team_list = document.getElementById("blue_team_list");
    blue_team_list.innerHTML = "";
    blue_team.forEach(player => {
        let li = document.createElement("li");
        li.textContent = player.id
        blue_team_list.appendChild(li);
    });
    drawPlayers(blue_team);
});

connection.on("ActualRedTeam", function (red_team) {
    console.log(red_team);
    red_team_players = red_team;
    let red_team_list = document.getElementById("red_team_list");
    red_team_list.innerHTML = "";
    red_team.forEach(player => {
        let li = document.createElement("li");
        li.textContent = player.id
        red_team_list.appendChild(li);
    });
    drawPlayers(red_team);
});

connection.on("UserConnected", function (connectionId) {
    let li = document.createElement("li");
    li.textContent = connectionId + " connected";
    document.getElementById("messagesList").appendChild(li);

});

connection.on("PlayerMoved", function (_id, X, Y) {
    let player = blue_team_players.find(player => player.id === _id);
    if(player === undefined) {
        player = red_team_players.find(player => player.id === _id);
    }
    player.coordinates.x = X;
    player.coordinates.y = Y;
    clearCanvas();
    drawPlayers(blue_team_players);
    drawPlayers(red_team_players);
});

function drawPlayer(player) {    
    ctx.fillStyle = player.team ? 'blue' : 'red';

    // Draw player as a circle
    ctx.beginPath();
    ctx.arc(player.coordinates.x, player.coordinates.y, 10, 0, Math.PI * 2);
    ctx.fill();
}

function clearCanvas() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
}

function drawPlayers(players) {
    players.forEach( player => drawPlayer(player));
}
