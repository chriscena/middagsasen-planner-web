CREATE TABLE [dbo].[UserCompetencies]
(
  UserCompetencyId int not null identity,
  constraint PK_UserCompetencies PRIMARY KEY (UserCompetencyId),
  UserId int not null,
  constraint FK_UserCompetencies_Users foreign key (UserId) references Users(UserId) on delete cascade,
  CompetencyId int not null,
  constraint FK_UserCompetencies_Competencies foreign key (CompetencyId) references Competencies(CompetencyId) on delete cascade,
  Approved bit not null constraint DF_UserCompetencies_Approved default 0,
  ApprovedDate datetime null,
  ApprovedBy int null,
  constraint FK_UserCompetencies_Users_ApprovedBy foreign key (ApprovedBy) references Users(UserId) on delete no action,
  ExpiryDate datetime null,
  Created datetime not null constraint DF_UserCompetencies_Created default GETUTCDATE(),
  constraint UQ_UserCompetencies_UserId_CompetencyId unique(UserId, CompetencyId),
)
