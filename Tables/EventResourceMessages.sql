CREATE TABLE [dbo].[EventResourceMessages]
(
  EventResourceMessageId int not null IDENTITY,
  constraint PK_EventResourceMessages PRIMARY key (EventResourceMessageId),
  EventResourceId int not null,
  constraint FK_EventResourceMessages_EventResources FOREIGN KEY (EventResourceId) REFERENCES EventResources(EventResourceId) on delete cascade,
  Message nvarchar(max) not null,
  Created datetime not null default GETUTCDATE(),
  CreatedBy int not null,
  constraint FK_EventResourceMessages_Users_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId) on delete cascade,
)
