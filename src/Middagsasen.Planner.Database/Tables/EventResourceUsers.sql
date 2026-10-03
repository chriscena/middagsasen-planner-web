create table EventResourceUsers (
    EventResourceUserId int not null IDENTITY,
    constraint PK_EventResourceUsers PRIMARY key (EventResourceUserId),
    UserId int not null,
    constraint FK_EventResourceUsers_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) on delete cascade,
    EventResourceId int not null,
    constraint FK_EventResourceUsers_EventResources FOREIGN KEY (EventResourceId) REFERENCES EventResources(EventResourceId) on delete cascade,
    StartTime datetime null,
    EndTime datetime null,
    Comment nvarchar(max) null,
)
GO

-- Samme bruker kan bare stå én gang på samme ressurs (vakt). Navnet brukes av ShiftRepository i API-et for å
-- kjenne igjen bruddet. Eksisterende duplikater fjernes av Scripts/Script.PreDeployment.sql før indeksen opprettes.
CREATE UNIQUE INDEX UQ_EventResourceUsers_EventResourceId_UserId ON EventResourceUsers (EventResourceId, UserId)
