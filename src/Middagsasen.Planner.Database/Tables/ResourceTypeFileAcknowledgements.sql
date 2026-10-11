-- Kvittering: at en bruker har bekreftet å ha lest og forstått en instruks (et vedlegg med RequiresAcknowledgement).
-- Kvitteringen gjelder den konkrete filen, så en ny versjon av instruksen krever ny kvittering. En rad endres aldri.
CREATE TABLE [dbo].[ResourceTypeFileAcknowledgements]
(
  ResourceTypeFileAcknowledgementId int not null identity,
  constraint PK_ResourceTypeFileAcknowledgements primary key (ResourceTypeFileAcknowledgementId),
  ResourceTypeFileId int not null,
  constraint FK_ResourceTypeFileAcknowledgements_ResourceTypeFiles foreign key (ResourceTypeFileId) references ResourceTypeFiles(ResourceTypeFileId),
  UserId int not null,
  constraint FK_ResourceTypeFileAcknowledgements_Users foreign key (UserId) references Users(UserId),
  constraint UQ_ResourceTypeFileAcknowledgements_UserId_ResourceTypeFileId unique (UserId, ResourceTypeFileId),
  -- Når brukeren kvitterte (UTC).
  Acknowledged datetime not null,
  Comment nvarchar(1000) null,
)
