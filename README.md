# BenchmarkNet
BenchmarkNet is a console application for testing networking libraries written by nxrighthere.

This fork is used for stress testing DarkRift 2 with BenchmarkNet before releases.

Please visit [BenchmarkNet's GitHub](https://github.com/nxrighthere/BenchmarkNet) for comparisons with other networking libraries and feel free to donate to the original author [![Bountysource](https://img.shields.io/badge/bountysource-donate-green.svg)](https://salt.bountysource.com/checkout/amount?team=nxrighthere)

Features:
- Asynchronous simulation of a large number of clients
- Stable under high loads
- Simple and flexible simulation setup
- Detailed session information

How it works?
--------
Each simulated client is one asynchronous task for establishing a connection with the server and processing the network events. Each task has one subtask which also works asynchronously to send network messages at a specified interval (15 messages per second by default). So, 1000 simulated clients is 1000 tasks with 1000 subtasks which works independently of each other. This sounds scary, but CPU usage is <1% for tasks itself and every operation is completely thread-safe. The clients send network messages to the server (500 reliable and 1000 unreliable by default). The server also sends messages to the clients in response (48 bytes per message by default). The application will monitor how the data is processed by the server and clients, and report their status in real-time.

Usage
--------
Run the application and enter the desired parameters to override the default values. Do not perform any actions while the benchmark is running and wait until the process is complete.

You can use packet sniffer to monitor how the data is transmitted, but it may affect the results.

If you want to simulate a bad network condition, use [Clumsy](http://jagt.github.io/clumsy/ "Clumsy") as an ideal companion.

Discussion
--------
Feel free to join BenchmarkNet's discussion in the [thread](https://forum.unity.com/threads/benchmarknet-stress-test-for-enet-unet-litenetlib-lidgren-and-miniudp.512507 "thread") on Unity forums.

If you have any questions, contact the original author via [email](mailto:nxrighthere@gmail.com "email") or Jamie (DarkRift) via [email](mailto:jamie:@darkriftnetworking.com).

Donations
--------
This project has already had an impact and helped developers in an improvement of their networking libraries. If you like this project, you can support the original author on [Bountysource](https://salt.bountysource.com/checkout/amount?team=nxrighthere), [Ko-fi](https://ko-fi.com/nxrighthere "Ko-fi") or [PayPal](https://www.paypal.me/nxrighthere "PayPal").

Any support is much appreciated by him.
