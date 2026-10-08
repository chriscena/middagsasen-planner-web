CREATE TABLE [dbo].[EventCompetencyRequirements]
(
  EventCompetencyRequirementId int not null identity,
  constraint PK_EventCompetencyRequirements PRIMARY KEY (EventCompetencyRequirementId),
  EventId int not null,
  constraint FK_EventCompetencyRequirements_Events foreign key (EventId) references Events(EventId) on delete cascade,
  CompetencyId int not null,
  constraint FK_EventCompetencyRequirements_Competencies foreign key (CompetencyId) references Competencies(CompetencyId) on delete cascade,
  MinimumRequired int not null,
  constraint UQ_EventCompetencyRequirements_EventId_CompetencyId unique(EventId, CompetencyId),
)
