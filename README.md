## Zero Bounce C# SDK
This SDK contains methods for interacting easily with ZeroBounce API.
More information about ZeroBounce you can find in the [official documentation](https://www.zerobounce.net/docs/).

## INSTALLATION
You can install by searching for ZeroBounceSDK in NuGet package manager browser or just use the this command:
```bash
Install-Package ZeroBounceSDK
```

## USAGE
Import the sdk in your file:
```c#
using ZeroBounceSDK;
``` 

Initialize the sdk with your api key:
```c# 
ZeroBounce.Instance.Initialize("<YOUR_API_KEY>");
```

## Examples
Then you can use any of the SDK methods, for example:

- Validate an email address
```c#
ZeroBounce.Instance.Validate("<EMAIL_TO_TEST>", "<OPTIONAL_IP_ADDRESS>",
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

- Check how many credits you have left on your account
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