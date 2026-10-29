QT += core gui qml quick quickcontrols2
CONFIG += c++17 release
TEMPLATE = app
isEmpty(TARGET): TARGET = omasharp-host
SOURCES += main.cpp devhost.cpp theme.cpp textdump.cpp drawview.cpp
HEADERS += omasharp.h
LIBS += -ldl
QMAKE_LFLAGS += -Wl,-rpath,\'\$\$ORIGIN\'
!isEmpty(APP_QRC): RESOURCES += $$APP_QRC
QMAKE_LFLAGS += -Wl,--export-dynamic-symbol=omasharp_host_run
