CREATE TABLE [dbo].[ResourceTypeTrainers]
(
  ResourceTypeTrainerId int not null identity,
  constraint PK_ResourceTypeTrainers PRIMARY KEY (ResourceTypeTrainerId),
  ResourceTypeId int not null,
  constraint FK_ResourceTypeTrainers_ResourceTypes foreign key (ResourceTypeId) references ResourceTypes(ResourceTypeId),
  UserId int not null,
  constraint FK_ResourceTypeTrainers_Users foreign key (UserId) references Users(UserId),
  constraint UQ_ResourceTypeTrainers_ResourceTypeId_UserId unique(ResourceTypeId, UserId),
  Inactive bit not null constraint DF_ResourceTypeTrainers_Inactive default 0,
)
