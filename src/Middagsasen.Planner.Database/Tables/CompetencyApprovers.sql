CREATE TABLE [dbo].[CompetencyApprovers]
(
  CompetencyApproverId int not null identity,
  constraint PK_CompetencyApprovers PRIMARY KEY (CompetencyApproverId),
  CompetencyId int not null,
  constraint FK_CompetencyApprovers_Competencies foreign key (CompetencyId) references Competencies(CompetencyId) on delete cascade,
  UserId int not null,
  constraint FK_CompetencyApprovers_Users foreign key (UserId) references Users(UserId) on delete cascade,
  constraint UQ_CompetencyApprovers_CompetencyId_UserId unique(CompetencyId, UserId),
  Inactive bit not null constraint DF_CompetencyApprovers_Inactive default 0,
)
