#### INSTALACIÓN
Puede instalar el SDK buscando ZeroBounceSDK en el navegador del administrador de paquetes NuGet o simplemente use el siguiente comando:
```bash
Install-Package ZeroBounce.SDK
```

#### USO
Importe el SDK en su archivo:
```c###
using ZeroBounceSDK;
``` 

Inicialice el SDK con su clave de API:
```c### 
ZeroBounce.Instance.Initialize("<SU_CLAVE_DE_API>");
```

#### Ejemplos
Luego puede utilizar cualquiera de los métodos del SDK, por ejemplo:
* ####### Validar una dirección de correo electrónico
```c###
var email = "<DIRECCIÓN_DE_CORREO_ELECTRÓNICO>";   // La dirección de correo electrónico que desea validar
var ipAddress = "127.0.0.1";     // La dirección IP desde la cual se registró el correo electrónico (opcional)

ZeroBounce.Instance.Validate(email, ipAddress,
    response =>
    {
        Debug.WriteLine("Validate success response " + response);
        // ... su implementación
    },
    error =>
    {
        Debug.WriteLine("Validate failure error " + error);
        // ... su implementación
    });
```

* ####### Verificar cuántos créditos le quedan en su cuenta
```c###
ZeroBounce.Instance.GetCredits(
    response =>
    {
        Debug.WriteLine("GetCredits success response " + response);
        // su implementación
    },
    error =>
    {
        Debug.WriteLine("GetCredits failure error " + error);
        // su implementación
    });
```

* ####### Verificar el uso de su API durante un período de tiempo específico
```c###
var startDate = new DateTime();    // La fecha de inicio de cuando desea ver el uso de la API
var endDate = new DateTime();      // La fecha de finalización de cuando desea ver el uso de la API

ZeroBounce.Instance.GetApiUsage(startDate, endDate,
    response =>
    {
        Debug.WriteLine("GetApiUsage success response " + response);
        // ... su implementación
    },
    error =>
    {
        Debug.WriteLine("GetApiUsage failure error " + error);
        // ... su implementación
    });
```

* ####### Utilice el endpoint de actividad para obtener información sobre el compromiso general de sus suscriptores por correo electrónico
```c###
var email = "valid@example.com";    // Dirección de correo electrónico del suscriptor

ZeroBounceTest.Instance.GetActivity(email,
    response =>
    {
        Debug.WriteLine("GetActivity success response " + response);
        // ... su implementación
    },
    error =>
    {
        Debug.WriteLine("GetActivity failure error " + error);
        // ... su implementación
    });
```

* ####### El API sendfile permite al usuario enviar un archivo para validación masiva de correo electrónico
```c###
var filePath = File("<RUTA_DEL_ARCHIVO>"); // El archivo CSV o TXT
var options = new SendFileOptions();

options.ReturnUrl = "https://domain.com/called/after/processing/request";
options.EmailAddressColumn=3            // El índice de columna "email" en el archivo. El índice comienza en 1
options.FirstNameColumn = 4;            // El índice de columna "first name" en el archivo
options.LastNameColumn = 5;             // El índice de columna "last name" en el archivo
options.GenderColumn = 6;               // El índice de columna "gender" en el archivo
options.IpAddressColumn = 7;            // El índice de columna "IP address" en el archivo
options.HasHeaderRow = true;            // Si esto es `true`, la primera fila se considera como encabezados de tabla

ZeroBounce.Instance.SendFile(
    filePath,
    options,
    response =>
    {
        Debug.WriteLine("SendFile success response " + response);
        // ... su implementación
    },
    error =>
    {
        Debug.WriteLine("SendFile failure error " + error);
        // ... su implementación
    });
```

* ####### El API getfile permite a los usuarios obtener el archivo de resultados de validación para el archivo que se envió utilizando el método sendfile
```c###
var fileId = "<ID_DE_ARCHIVO>";                       // El ID de archivo devuelto al llamar al API sendfile
var localDownloadPath = "<RUTA_DE_DESCARGA_DEL_ARCHIVO>"; // La ubicación donde se guardará el archivo descargado

ZeroBounce.Instance.GetFile(fileId, localDownloadPath,
    response =>
    {
        Debug.WriteLine("GetFile success response " + response);
        // ... su implementación
    },
    error =>
    {
        Debug.WriteLine("GetFile failure error " + error);
        // ... su implementación
    });
```

* ####### Verificar el estado de un archivo cargado mediante el método "sendFile"
```c###
var fileId = "<ID_DE_ARCHIVO>";                       // El ID de archivo devuelto al llamar al API sendfile

ZeroBounce.Instance.FileStatus(fileId,
    response =>
    {
        Debug.WriteLine("FileStatus success response " + response);
        // ... su implementación
    },
    error =>
    {
        Debug.WriteLine("FileStatus failure error " + error);
        // ... su implementación
    });
```

* ####### Eliminar el archivo que se envió utilizando el método "sendFile". El archivo solo se puede eliminar cuando su estado es _`Completado`_
```c###
var fileId = "<ID_DE_ARCHIVO>";                       // El ID de archivo devuelto al llamar al API sendfile

ZeroBounce.Instance.DeleteFile(fileId,
    response =>
    {
        Debug.WriteLine("DeleteFile success response " + response);
        // ... su implementación
    },
    error =>
    {
        Debug.WriteLine("DeleteFile failure error " + error);
        // ... su implementación
    });
```

##### API de puntuación de inteligencia artificial

* ####### El API scoringSendfile permite al usuario enviar un archivo para la puntuación masiva de correo electrónico
```c###
var filePath = File("<RUTA_DEL_ARCHIVO>"); // El archivo CSV o TXT

var options = new SendFileOptions();

options.ReturnUrl = "https://domain.com/called/after/processing/request";
options.EmailAddressColumn=3            // El índice de columna "email" en el archivo. El índice comienza en 1
options.HasHeaderRow = true;            // Si esto es `true`, la primera fila se considera como encabezados de tabla


ZeroBounce.Instance.ScoringSendFile(
    filePath,
    options,
    response =>
    {
        Debug.WriteLine("SendFile success response " + response);
        // ... su implementación
    },
    error =>
    {
        Debug.WriteLine("SendFile failure error " + error);
        // ... su implementación
    });
```

* ####### El API scoringGetFile permite a los usuarios obtener el archivo de resultados de validación para el archivo que se envió utilizando el método scoringSendfile
```c###
var fileId = "<ID_DE_ARCHIVO>";                       // El ID de archivo devuelto al llamar al API scoringSendfile
var localDownloadPath = "<RUTA_DE_DESCARGA_DEL_ARCHIVO>"; // La ubicación donde se guardará el archivo descargado

ZeroBounce.Instance.ScoringGetFile(fileId, localDownloadPath,
    response =>
    {
        Debug.WriteLine("GetFile success response " + response);
        // ... su implementación
    },
    error =>
    {
        Debug.WriteLine("GetFile failure error " + error);
        // ... su implementación
    });
```

* ####### Verificar el estado de un archivo cargado mediante el método "scoringSendFile"
```c###
var fileId = "<ID_DE_ARCHIVO>";                       // El ID de archivo devuelto al llamar al API scoringSendfile

ZeroBounce.Instance.ScoringFileStatus(fileId,
    response =>
    {
        Debug.WriteLine("FileStatus success response " + response);
        // ... su implementación
    },
    error =>
    {
        Debug.WriteLine("FileStatus failure error " + error);
        // ... su implementación
    });
```

* ####### Eliminar el archivo que se envió utilizando el método "scoringSendFile". El archivo solo se puede eliminar cuando su estado es _`Completado`_
```c###
var fileId = "<ID_DE_ARCHIVO>";                       // El ID de archivo devuelto al llamar al API scoringSendfile

ZeroBounce.Instance.ScoringDeleteFile(fileId,
    response =>
    {
        Debug.WriteLine("DeleteFile success response " + response);
        // ... su implementación
    },
    error =>
    {
        Debug.WriteLine("DeleteFile failure error " + error);
        // ... su implementación
    });
```
