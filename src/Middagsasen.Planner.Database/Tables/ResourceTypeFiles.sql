CREATE TABLE [dbo].[ResourceTypeFiles]
(        
  ResourceTypeFileId int not null identity,
  constraint PK_ResourceTypeFiles PRIMARY key (ResourceTypeFileId),
  StorageName nvarchar(400) not null, 
  FileName nvarchar(400) not null,
  Description nvarchar(1000) not null,
  MimeType nvarchar(400) not null, 
  ResourceTypeId int not null,
  constraint FK_ResourceTypeFiles_ResourceTypes foreign key (ResourceTypeId) references ResourceTypes(ResourceTypeId),
  Created datetime not null,
  CreatedBy int not null,
  constraint FK_ResourceTypeFiles_Users_CreatedBy foreign key (CreatedBy) references Users(UserId),
  Updated datetime not null,
  UpdatedBy int not null,
   constraint FK_ResourceTypeFiles_Users_UpdatedBy foreign key (UpdatedBy) references Users(UserId),
)
