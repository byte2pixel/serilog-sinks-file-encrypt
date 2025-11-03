# Example .NET Console Application

This example demonstrates using the encrypted logging in a .NET Console application.

These are example keys you can find the public key in the `public_key.xml` file.
The private key in `private_key.xml` is for demonstration purposes only and should not be used in production environments.

# Prerequisites
- .NET SDK installed on your machine. You can download it from [here](https://dotnet.microsoft.com/download).
- Basic knowledge of C# and .NET Console applications.
- Familiarity with logging concepts.
- Access to the public and private key files (`public_key.xml` and `private_key.xml`).

# Setup Instructions
1. Clone the repository or download the example code to your local machine.
2. Open the project in your preferred IDE (e.g., Visual Studio, Rider, VS Code).
3. Ensure that the `public_key.xml` and `private_key.xml` files are included in the project directory.
4. Restore the NuGet packages by running the following command in the terminal:
5. ```bash
   dotnet restore
   ```
6. Build the project using the following command:
7. ```bash
   dotnet build
   ```

# Running the Application
1. To run the console application, use the following command:
2. ```bash
   dotnet run --project Example.Console.csproj
   ```
3. The application will execute and log messages using the encrypted logging mechanism.
4. You should see log messages in the console output along with the output path for the encrypted log file.
5. To verify the encrypted log, you can check the log file generated in bin/Debug/net8.0/Logs directory.
6. Use the private key to decrypt the logs if necessary, using the CLI tool provided in the main project.
7. Refer to the CLI project documentation for instructions on decrypting logs.
