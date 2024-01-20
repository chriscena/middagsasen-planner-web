CREATE TABLE [dbo].[ResourceTypeTrainings]
(
  ResourceTypeTrainingId int not null identity,
  constraint PK_ResourceTypeTrainings PRIMARY KEY (ResourceTypeTrainingId),
  UserId int not null,
  constraint FK_ResourceTypeTrainings_Users foreign key (UserId) references Users(UserId),
  ResourceTypeId int not null,
  constraint FK_ResourceTypeTrainings_ResourceTypes foreign key (ResourceTypeId) references ResourceTypes(ResourceTypeId),
  constraint UQ_ResourceTypeTrainings_UserId_ResourceTypeId unique(UserId, ResourceTypeId),
  TrainingComplete bit not null constraint DF_ResourceTypeTrainings_TrainingComplete default 0,
  Confirmed datetime null,
  ConfirmedBy int null,
  constraint FK_ResourceTypeTrainings_Users_ConfirmedBy foreign key (ConfirmedBy) references Users(UserId),
)