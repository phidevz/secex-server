# SecEx - Secure File Exchange

[![Docker Build](https://github.com/phidevz/secex-server/actions/workflows/docker-publish.yml/badge.svg)](https://github.com/phidevz/secex-server/actions/workflows/docker-publish.yml)

This is a reference implementation for the server to the SecEx frontend, which you can find at 
[phidevz/secex](https://github.com/phidevz/secex).

## Exchange encrypted files with anyone without - all they need is a browser.

One time or another you might want to exchange files with a third party, but they cannot install any software on their 
computer, e.g. on their work computer. If this data is **very sensitive**, you probably won't trust any of the major 
cloud providers and just upload the data there. You want to serve the data encrypted and maybe even proof to the third 
party that actually **you** are sending them this file.

Using the **GNU Privacy Guard (GPG)** technology, you can serve password-encrypted and signed version of your sensitive 
files to anyone. The files are decrypted and verified only in the recipient's browser, so they are truely **end-to-end 
encrypted** and cannot be read by anyone else.

## How to run the application

You can run the application either using Docker, or standalone.

**Important:** _By default_, browsing download directories is **disabled** in the application for security reasons. If 
you want to **enable** this functionality (i.e. the end user can see all files by name which you serve in a download 
directory), set the environment variable `SECEX_ENABLE_BROWSE_FILES` to `true`. This applies to both Docker and 
standalone deployment.

### Using Docker

1. You need a Docker volume or host path to persist the data uploaded to and served from the application as well as the 
   GPG keys.
2. `docker run -d --name secex-server --restart always -v secex-data:/app/store -p 80:80 ghcr.io/phidevz/secex-server:latest`

Available images are tagged major.minor.patch SemVer v2 (e.g. `1.2.3`) and `latest`.
See all available images [on GitHub](https://github.com/phidevz/secex-server/pkgs/container/secex-server/versions).

You can find the Dockerfile used to build the container image [here](Dockerfile).

For information on Docker run parameters, please have a look at the 
[Docker Command Line Reference](https://docs.docker.com/engine/reference/commandline/run/).

If you want to run the container using Docker compose, please refer to the 
[Docker Compose v2 reference](https://docs.docker.com/compose/compose-file/deploy/).

### Without Docker

As I will not provide the built binary separately, you will need either Docker to extract the Linux binary files from 
one of the container images or [build the application yourself](#build-for-production) (if you e.g. want binaries for 
Windows or MacOS).

Make sure that you have the ASP.NET 6 Core Runtime installed on the system you want to execute the server on.

The application uses the current working directory as the root to serve downloads and keys as well as accept uploads. 
When not running in a container, this path will probably be the directory where you place the server files. You probably
want to adapt this accordingly.

You will likely want to specify another port and/or IP to bind to as `0.0.0.0:80` (or rather specifically: `http://+:80`). 
To achieve this, append the command line argument `--urls=http://1.2.3.4:8080`.

### Application configuration

For both running inside Docker and running the server without docker, you can also specify other and more complex configuration
options. **secex-server does not expose any other configuration options than above-mentioned "browse files" switch.**

Most notably, you can change the base URL under which the server acts using the environment variable `Kestrel__PathBase=/api`.
This would allow you to easily server your application using a reverse proxy (like NGINX, Apache2 or Caddy) as follows:
* The [frontend](https://github.com/phidevz/secex) would be accessible e.g. at `https://secure-exchange.example.com`
* The frontend would call the backend at `https://secure-exchange.example.com/api`; this way you do not have to pay respect
  to Cross-origin Resource Sharing (CORS).

For all other configuration, like log levels, please refer to the official 
[Microsoft documentation](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-6.0).

## Build for Production

If you want to or need to build the application yourself, you need the .NET 6 SDK.

In the project directory, run:

```
dotnet publish -c Release --no-self-contained -o bin/publish
```

This will place all binaries files into the `bin/publish` folder under the project directory. To install the software,
just copy the files to the target system. As explained [above](#without-docker), you need an appropriate runtime installed
on the target system.

## Like this project?

Glad you like it! I would greatly appreciate a star on Github, and feel free to share it with your friends and colleagues.

## Protocol

You can find an [Open API Specification v3](https://swagger.io/docs/specification/about/) of the frontend-backend 
protocol [here](https://github.com/phidevz/secex/blob/main/doc/protocol.yaml).

You can use [Swagger UI](https://swagger.io/tools/swagger-ui/) or the [Swagger Editor](https://editor.swagger.io) to 
visualize the specification.

## How to contribute

Found a bug? Want to contirbute new feature? You want to help, great!

Please open an issue and describe either

- the problem(s) you experience
- the features you want to request (no guarantee that it will be implemented)
- the features you want to implement

For smaller changes to the code, feel free to fork the code, make your change and contribute them back by opening a 
pull request.

## Found a security issue?

If you think you found a security issue, please send me a DM on [Twitter](https://twitter.com/phidevz) and I will get 
in touch with you.

## License

This project is licensed under the [GPL-3.0](LICENSE).
