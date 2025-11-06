## Zero Bounce C# SDK
This SDK contains methods for interacting easily with ZeroBounce API.
More information about ZeroBounce you can find in the [official documentation](https://www.zerobounce.net/docs/).

## INSTALLATION
You can install by searching for ZeroBounceSDK in NuGet package manager browser or just use the this command:
```bash
Install-Package ZeroBounce.SDK
```

## USAGE
Import the sdk in your file:
```c#
using ZeroBounceSDK;
``` 

Initialize the wrapper with your api key and preferred api:
```c# 
ZeroBounce.Instance.Initialize("<YOUR_API_KEY>", ZBApiURL.ApiDefaultURL);
```

## Examples
Then you can use any of the SDK methods, for example:
* ##### Validate an email address
```c#
var email = "<EMAIL_ADDRESS>";   // The email address you want to validate
var ipAddress = "127.0.0.1";     // The IP Address the email signed up from (Optional)

ZeroBounce.Instance.Validate(email, ipAddress,
    response =>
    {
        Debug.WriteLine("Validate success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("Validate failure error " + error);
        // ... your implementation
    });
```

* ##### Validate a batch of email addresses
```c#
var email = "<EMAIL_ADDRESS>";   // The email address you want to validate
var ipAddress = "127.0.0.1";     // The IP Address the email signed up from (Optional)

List<ZBValidateEmailRow> emailBatch = new List<ZBValidateEmailRow>
{ 
    new ZBValidateEmailRow { EmailAddress = email, IpAddress = ipAddress }
};
ZeroBounce.Instance.ValidateBatch(emailBatch,
    response =>
    {
        Debug.WriteLine(response.EmailBatch[0].Address);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("Validate failure error " + error);
        // ... your implementation
    });
```

* ##### Check how many credits you have left on your account
```c#
ZeroBounce.Instance.GetCredits(
    response =>
    {
        Debug.WriteLine("GetCredits success response " + response);
        // your implementation
    },
    error =>
    {
        Debug.WriteLine("GetCredits failure error " + error);
        // your implementation
    });
```

* ##### Check your API usage for a given period of time
```c#
var startDate = new DateTime();    // The start date of when you want to view API usage
var endDate = new DateTime();      // The end date of when you want to view API usage

ZeroBounce.Instance.GetApiUsage(startDate, endDate,
    response =>
    {
        Debug.WriteLine("GetApiUsage success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("GetApiUsage failure error " + error);
        // ... your implementation
    });
```

* ##### Use the Activity API endpoint to gather insights into your subscribers'overall email engagement
```c#
var email = "valid@example.com";    // Subscriber email address

ZeroBounce.Instance.GetActivity(email,
    response =>
    {
        Debug.WriteLine("GetActivity success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("GetActivity failure error " + error);
        // ... your implementation
    });
```

* ##### Use the Email Finder API endpoint to identify the correct email format when you provide a name and email domain or company name.
```c###
var domain = "example.com";  // The email domain for which to find the email format.
var companyName = "Example"; // The company name for which to find the email format.
var firstName = "john";      // The first name of the person whose email format is being searched.
var middleName = "";         // The middle name of the person whose email format is being searched. [optional]
var lastName = "doe";        // The last name of the person whose email format is being searched. [optional]

ZeroBounce.Instance.FindEmailByDomain(domain, firstName, middleName, lastName,
    response =>
    {
        Debug.WriteLine("EmailFinder success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("EmailFinder failure error " + error);
        // ... your implementation
    });

ZeroBounce.Instance.FindEmailByCompanyName(companyName, firstName, middleName, lastName,
    response =>
    {
        Debug.WriteLine("EmailFinder success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("EmailFinder failure error " + error);
        // ... your implementation
    });
```

* ##### Use the Domain Search API endpoint to identify the email domain when you provide a domain or company name.
```c###
var domain = "example.com";  // The email domain for which to find the email format.
var companyName = "Example"; // The company name for which to find the email format.

ZeroBounce.Instance.FindDomainByDomain(domain,
    response =>
    {
        Debug.WriteLine("DomainSearch success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("DomainSearch failure error " + error);
        // ... your implementation
    });

ZeroBounce.Instance.FindDomainByCompanyName(companyName,
    response =>
    {
        Debug.WriteLine("DomainSearch success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("DomainSearch failure error " + error);
        // ... your implementation
    });
```

* ##### The sendfile API allows user to send a file for bulk email validation
```c#
var filePath = "<FILE_PATH>"; // The csv or txt file
var options = new SendFileOptions();

options.ReturnUrl = "https://domain.com/called/after/processing/request";
options.EmailAddressColumn=3;           // The index of "email" column in the file. Index starts at 1
options.FirstNameColumn = 4;            // The index of "first name" column in the file
options.LastNameColumn = 5;             // The index of "last name" column in the file
options.GenderColumn = 6;               // The index of "gender" column in the file
options.IpAddressColumn = 7;            // The index of "IP address" column in the file
options.HasHeaderRow = true;            // If this is `true` the first row is considered as table headers
options.RemoveDuplicate = false;        // If you want the system to remove duplicate emails (true or false, default is true). Please note that if we remove more than 50% of the lines because of duplicates (parameter is true), system will return a 400 bad request error as a safety net to let you know that more than 50% of the file has been modified.

ZeroBounce.Instance.SendFile(
    filePath,
    options,
    response =>
    {
        Debug.WriteLine("SendFile success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("SendFile failure error " + error);
        // ... your implementation
    });
```

* ##### The getfile API allows users to get the validation results file for the file been submitted using sendfile API
```c#
var fileId = "<FILE_ID>";                       // The returned file ID when calling sendfile API
var localDownloadPath = "<FILE_DOWNLOAD_PATH>"; // The location where the downloaded file will be saved

ZeroBounce.Instance.GetFile(fileId, localDownloadPath,
    response =>
    {
        Debug.WriteLine("GetFile success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("GetFile failure error " + error);
        // ... your implementation
    });
```

* ##### Check the status of a file uploaded via "sendFile" method
```c#
var fileId = "<FILE_ID>";                       // The returned file ID when calling sendfile API

ZeroBounce.Instance.FileStatus(fileId,
    response =>
    {
        Debug.WriteLine("FileStatus success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("FileStatus failure error " + error);
        // ... your implementation
    });
```

* ##### Deletes the file that was submitted using scoring sendfile API. File can be deleted only when its status is _`Complete`_
```c#
var fileId = "<FILE_ID>";                       // The returned file ID when calling sendfile API

ZeroBounce.Instance.DeleteFile(fileId,
    response =>
    {
        Debug.WriteLine("DeleteFile success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("DeleteFile failure error " + error);
        // ... your implementation
    });
```

### AI Scoring API

* ##### The scoringSendfile API allows user to send a file for bulk email validation
```c#
var filePath = "<FILE_PATH>"; // The csv or txt file

var options = new SendFileOptions();

options.ReturnUrl = "https://domain.com/called/after/processing/request";
options.EmailAddressColumn=3;           // The index of "email" column in the file. Index starts at 1
options.HasHeaderRow = true;            // If this is `true` the first row is considered as table headers


ZeroBounce.Instance.ScoringSendFile(
    filePath,
    options,
    response =>
    {
        Debug.WriteLine("SendFile success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("SendFile failure error " + error);
        // ... your implementation
    });
```

* ##### The scoringGetFile API allows users to get the validation results file for the file been submitted using scoringSendfile API
```c#
var fileId = "<FILE_ID>";                       // The returned file ID when calling scoringSendfile API
var localDownloadPath = "<FILE_DOWNLOAD_PATH>"; // The location where the downloaded file will be saved

ZeroBounce.Instance.ScoringGetFile(fileId, localDownloadPath,
    response =>
    {
        Debug.WriteLine("GetFile success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("GetFile failure error " + error);
        // ... your implementation
    });
```

* ##### Check the status of a file uploaded via "scoringSendFile" method
```c#
var fileId = "<FILE_ID>";                       // The returned file ID when calling scoringSendfile API

ZeroBounce.Instance.ScoringFileStatus(fileId,
    response =>
    {
        Debug.WriteLine("FileStatus success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("FileStatus failure error " + error);
        // ... your implementation
    });
```

* ##### Deletes the file that was submitted using scoring scoringSendfile API. File can be deleted only when its status is _`Complete`_
```c#
var fileId = "<FILE_ID>";                       // The returned file ID when calling scoringSendfile API

ZeroBounce.Instance.ScoringDeleteFile(fileId,
    response =>
    {
        Debug.WriteLine("DeleteFile success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("DeleteFile failure error " + error);
        // ...your implementation
    });
```
