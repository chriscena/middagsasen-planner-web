create table UserSessions (
    UserSessionId UNIQUEIDENTIFIER not null,
    CONSTRAINT PK_UserSessions PRIMARY KEY (UserSessionId),
    UserId int not null,
    CONSTRAINT FK_UserSessions_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    Created DATETIME not null CONSTRAINT DF_UserSessions_Created DEFAULT GETUTCDATE(),
    AuthType int not null
)