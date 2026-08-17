# Microsoft Store certification notes

## Version 1.1.4.0 submission note

Use the following text in the certification notes for the new submission:

> The Microsoft Store package no longer declares the unvirtualizedResources restricted capability or disables registry write virtualization. Direct modification of File Explorer registry settings is disabled whenever WinBridge runs with package identity. The Store version now opens the standard Windows File Explorer Options interface so users can change file name extension and hidden-file visibility themselves. The unpackaged EXE and portable ZIP distributions retain the direct per-user setting feature. WinBridge runs asInvoker and does not request elevation.

`runFullTrust` remains declared because WinBridge is a packaged classic WPF desktop application. It is used to open standard Windows Settings and Control Panel pages and to run documented Windows system tools. The app does not elevate itself, install services or drivers, disable Windows security features, or write machine-wide registry values.

## Partner Center checklist

- Upload `WinBridge-v1.1.4.0-x64.msix` from the newly generated `WinBridge-msix-v1.1.4.0` directory.
- Under **Properties > Support info > Use different details for this app**, add a monitored support email address and/or a public developer website.
- The repository URL `https://github.com/mitarashi-dango/WinBridge` can be used as the developer website only while it is publicly accessible and contains current support information.
- Confirm that the restricted-capability justification no longer requests `unvirtualizedResources`.
- Add the submission note above and submit the updated package for certification.

## Package verification

The generated `AppxManifest.xml` must:

- contain `runFullTrust`;
- not contain `unvirtualizedResources`;
- not contain `RegistryWriteVirtualization`.
