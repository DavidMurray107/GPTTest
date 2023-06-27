﻿"use strict";
let connection = new signalR.HubConnectionBuilder().withUrl("/chatHub").build();
 
connection.on('start', function(){document.getElementById('ConnectionId').textContent = connection.connectionId;});
connection.start().then(function () {
    document.getElementById("sendButton").disabled = false;
    
}).catch(function (err) {
    return console.error(err.toString());
});

connection.on("ReceiveMessage", function (user, message) {
    
    let li = document.createElement("li");
    li.textContent = `${user} says ${message}`;
    document.getElementById("messagesList").appendChild(li);
});

document.getElementById("sendButton").addEventListener("click", function (event) {
    let user = document.getElementById("userInput").value;
    let message = document.getElementById("messageInput").value;
    connection.invoke("SendMessage", user, message).catch(function (err) {
        return console.error(err.toString());
    });
    event.preventDefault();
});