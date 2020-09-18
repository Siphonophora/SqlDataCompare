- [Privacy and Security](#privacy-and-security)
  - [Your SQL never leaves your browser.](#your-sql-never-leaves-your-browser)
  - [Are the comparison templates safe?](#are-the-comparison-templates-safe)
  - [Analytics](#analytics)

# Privacy and Security

Below are a few notes on privacey and security related to using this app.

## Your SQL never leaves your browser.

If you are using the web app [hosted here][webapp] you may wonder if the sql you are pasting into the app is sent to a remote server somewhere to be processed. The answer is no. All of the code for this utility runs locally in your browser. As a result **your SQL never leaves your machine**. The specific technology is [Blazor Web Assembly](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor). 

You are, of course, welcome to run the application locally on your own machine.

## Are the comparison templates safe?

As a rule, taking code off the internet and running it on your own systems is not a great idea. This utility does two things to help ensure that the templates it produces are safe and trustworthy. 

1. The first, and simplest to verify, is that the whole template is wrapped in a transaction that is rolled back. You can see the script begins with `BEGIN TRAN` and ends with `ROLLBACK TRAN`. Because there is no `COMMIT TRAN` anywhere in the output script, you can be confident that no perminent changes could happen as a result of running the script.
2. The utility attempts to detect any sql which has side effects. For example, it checks for attemps to insert, update or delete to real tables or DML statements that would change the database itself. When these statements are encountered, the utility will output a warning and will not produce a script. On the other hand, it is allowable to create and work with temp tables in the template.

## Analytics

Because this app does not do any other logging, Google Analytics is used on the [hosted version][webapp] to track usage data.

[webapp]: https://sqldatacompare.mjconrad.com/