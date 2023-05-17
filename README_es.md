{\rtf1\ansi\ansicpg1252\cocoartf2709
\cocoatextscaling0\cocoaplatform0{\fonttbl\f0\fswiss\fcharset0 Helvetica;}
{\colortbl;\red255\green255\blue255;}
{\*\expandedcolortbl;;}
\paperw11900\paperh16840\margl1440\margr1440\vieww11520\viewh8400\viewkind0
\pard\tx566\tx1133\tx1700\tx2267\tx2834\tx3401\tx3968\tx4535\tx5102\tx5669\tx6236\tx6803\pardirnatural\partightenfactor0

\f0\fs24 \cf0 ## ZeroBounce SDK para C#\
\
Este SDK contiene m\'e9todos para interactuar f\'e1cilmente con la API de ZeroBounce.\
Puede encontrar m\'e1s informaci\'f3n sobre ZeroBounce en la [documentaci\'f3n oficial](https://www.zerobounce.net/docs/).\
\
## INSTALACI\'d3N\
Puede instalar el SDK buscando ZeroBounceSDK en el navegador del administrador de paquetes NuGet o simplemente use el siguiente comando:\
```bash\
Install-Package ZeroBounce.SDK\
```\
\
## USO\
Importe el SDK en su archivo:\
```c#\
using ZeroBounceSDK;\
``` \
\
Inicialice el SDK con su clave de API:\
```c# \
ZeroBounce.Instance.Initialize("<SU_CLAVE_DE_API>");\
```\
\
## Ejemplos\
Luego puede utilizar cualquiera de los m\'e9todos del SDK, por ejemplo:\
* ##### Validar una direcci\'f3n de correo electr\'f3nico\
```c#\
var email = "<DIRECCI\'d3N_DE_CORREO_ELECTR\'d3NICO>";   // La direcci\'f3n de correo electr\'f3nico que desea validar\
var ipAddress = "127.0.0.1";     // La direcci\'f3n IP desde la cual se registr\'f3 el correo electr\'f3nico (opcional)\
\
ZeroBounce.Instance.Validate(email, ipAddress,\
    response =>\
    \{\
        Debug.WriteLine("Validate success response " + response);\
        // ... su implementaci\'f3n\
    \},\
    error =>\
    \{\
        Debug.WriteLine("Validate failure error " + error);\
        // ... su implementaci\'f3n\
    \});\
```\
\
* ##### Verificar cu\'e1ntos cr\'e9ditos le quedan en su cuenta\
```c#\
ZeroBounce.Instance.GetCredits(\
    response =>\
    \{\
        Debug.WriteLine("GetCredits success response " + response);\
        // su implementaci\'f3n\
    \},\
    error =>\
    \{\
        Debug.WriteLine("GetCredits failure error " + error);\
        // su implementaci\'f3n\
    \});\
```\
\
* ##### Verificar el uso de su API durante un per\'edodo de tiempo espec\'edfico\
```c#\
var startDate = new DateTime();    // La fecha de inicio de cuando desea ver el uso de la API\
var endDate = new DateTime();      // La fecha de finalizaci\'f3n de cuando desea ver el uso de la API\
\
ZeroBounce.Instance.GetApiUsage(startDate, endDate,\
    response =>\
    \{\
        Debug.WriteLine("GetApiUsage success response " + response);\
        // ... su implementaci\'f3n\
    \},\
    error =>\
    \{\
        Debug.WriteLine("GetApiUsage failure error " + error);\
        // ... su implementaci\'f3n\
    \});\
```\
\
* ##### Utilice el endpoint de actividad para obtener informaci\'f3n sobre el compromiso general de sus suscriptores por correo electr\'f3nico\
```c#\
var email = "valid@example.com";    // Direcci\'f3n de correo electr\'f3nico del suscriptor\
\
ZeroBounceTest.Instance.GetActivity(email,\
    response =>\
    \{\
        Debug.WriteLine("GetActivity success response " + response);\
        // ... su implementaci\'f3n\
    \},\
    error =>\
    \{\
        Debug.WriteLine("GetActivity failure error " + error);\
        // ... su implementaci\'f3n\
    \});\
```\
\
* ##### El API sendfile permite al usuario enviar un archivo para validaci\'f3n masiva de correo electr\'f3nico\
```c#\
var filePath = File("<RUTA_DEL_ARCHIVO>"); // El archivo CSV o TXT\
var options = new SendFileOptions();\
\
options.ReturnUrl = "https://domain.com/called/after/processing/request";\
options.EmailAddressColumn=3            // El \'edndice de columna "email" en el archivo. El \'edndice comienza en 1\
options.FirstNameColumn = 4;            // El \'edndice de columna "first name" en el archivo\
options.LastNameColumn = 5;             // El \'edndice de columna\
\
 "last name" en el archivo\
options.GenderColumn = 6;               // El \'edndice de columna "gender" en el archivo\
options.IpAddressColumn = 7;            // El \'edndice de columna "IP address" en el archivo\
options.HasHeaderRow = true;            // Si esto es `true`, la primera fila se considera como encabezados de tabla\
\
ZeroBounce.Instance.SendFile(\
    filePath,\
    options,\
    response =>\
    \{\
        Debug.WriteLine("SendFile success response " + response);\
        // ... su implementaci\'f3n\
    \},\
    error =>\
    \{\
        Debug.WriteLine("SendFile failure error " + error);\
        // ... su implementaci\'f3n\
    \});\
```\
\
* ##### El API getfile permite a los usuarios obtener el archivo de resultados de validaci\'f3n para el archivo que se envi\'f3 utilizando el m\'e9todo sendfile\
```c#\
var fileId = "<ID_DE_ARCHIVO>";                       // El ID de archivo devuelto al llamar al API sendfile\
var localDownloadPath = "<RUTA_DE_DESCARGA_DEL_ARCHIVO>"; // La ubicaci\'f3n donde se guardar\'e1 el archivo descargado\
\
ZeroBounce.Instance.GetFile(fileId, localDownloadPath,\
    response =>\
    \{\
        Debug.WriteLine("GetFile success response " + response);\
        // ... su implementaci\'f3n\
    \},\
    error =>\
    \{\
        Debug.WriteLine("GetFile failure error " + error);\
        // ... su implementaci\'f3n\
    \});\
```\
\
* ##### Verificar el estado de un archivo cargado mediante el m\'e9todo "sendFile"\
```c#\
var fileId = "<ID_DE_ARCHIVO>";                       // El ID de archivo devuelto al llamar al API sendfile\
\
ZeroBounce.Instance.FileStatus(fileId,\
    response =>\
    \{\
        Debug.WriteLine("FileStatus success response " + response);\
        // ... su implementaci\'f3n\
    \},\
    error =>\
    \{\
        Debug.WriteLine("FileStatus failure error " + error);\
        // ... su implementaci\'f3n\
    \});\
```\
\
* ##### Eliminar el archivo que se envi\'f3 utilizando el m\'e9todo "sendFile". El archivo solo se puede eliminar cuando su estado es _`Completado`_\
```c#\
var fileId = "<ID_DE_ARCHIVO>";                       // El ID de archivo devuelto al llamar al API sendfile\
\
ZeroBounce.Instance.DeleteFile(fileId,\
    response =>\
    \{\
        Debug.WriteLine("DeleteFile success response " + response);\
        // ... su implementaci\'f3n\
    \},\
    error =>\
    \{\
        Debug.WriteLine("DeleteFile failure error " + error);\
        // ... su implementaci\'f3n\
    \});\
```\
\
### API de puntuaci\'f3n de inteligencia artificial\
\
* ##### El API scoringSendfile permite al usuario enviar un archivo para la puntuaci\'f3n masiva de correo electr\'f3nico\
```c#\
var filePath = File("<RUTA_DEL_ARCHIVO>"); // El archivo CSV o TXT\
\
var options = new SendFileOptions();\
\
options.ReturnUrl = "https://domain.com/called/after/processing/request";\
options.EmailAddressColumn=3            // El \'edndice de columna "email" en el archivo. El \'edndice comienza en 1\
options.HasHeaderRow = true;            // Si esto es `true`, la primera fila se considera como encabezados de tabla\
\
\
ZeroBounce.Instance.ScoringSendFile(\
    filePath,\
    options,\
    response =>\
    \{\
        Debug.WriteLine("SendFile success response " + response);\
        // ... su implementaci\'f3n\
    \},\
    error =>\
    \{\
        Debug.WriteLine("SendFile failure error " + error);\
        // ... su implementaci\'f3n\
    \});\
```\
\
* ##### El API scoringGetFile permite a los usuarios obtener\
\
 el archivo de resultados de validaci\'f3n para el archivo que se envi\'f3 utilizando el m\'e9todo scoringSendfile\
```c#\
var fileId = "<ID_DE_ARCHIVO>";                       // El ID de archivo devuelto al llamar al API scoringSendfile\
var localDownloadPath = "<RUTA_DE_DESCARGA_DEL_ARCHIVO>"; // La ubicaci\'f3n donde se guardar\'e1 el archivo descargado\
\
ZeroBounce.Instance.ScoringGetFile(fileId, localDownloadPath,\
    response =>\
    \{\
        Debug.WriteLine("GetFile success response " + response);\
        // ... su implementaci\'f3n\
    \},\
    error =>\
    \{\
        Debug.WriteLine("GetFile failure error " + error);\
        // ... su implementaci\'f3n\
    \});\
```\
\
* ##### Verificar el estado de un archivo cargado mediante el m\'e9todo "scoringSendFile"\
```c#\
var fileId = "<ID_DE_ARCHIVO>";                       // El ID de archivo devuelto al llamar al API scoringSendfile\
\
ZeroBounce.Instance.ScoringFileStatus(fileId,\
    response =>\
    \{\
        Debug.WriteLine("FileStatus success response " + response);\
        // ... su implementaci\'f3n\
    \},\
    error =>\
    \{\
        Debug.WriteLine("FileStatus failure error " + error);\
        // ... su implementaci\'f3n\
    \});\
```\
\
* ##### Eliminar el archivo que se envi\'f3 utilizando el m\'e9todo "scoringSendFile". El archivo solo se puede eliminar cuando su estado es _`Completado`_\
```c#\
var fileId = "<ID_DE_ARCHIVO>";                       // El ID de archivo devuelto al llamar al API scoringSendfile\
\
ZeroBounce.Instance.ScoringDeleteFile(fileId,\
    response =>\
    \{\
        Debug.WriteLine("DeleteFile success response " + response);\
        // ... su implementaci\'f3n\
    \},\
    error =>\
    \{\
        Debug.WriteLine("DeleteFile failure error " + error);\
        // ... su implementaci\'f3n\
    \});\
```}