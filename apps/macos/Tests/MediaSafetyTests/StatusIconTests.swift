import AppKit
import XCTest
@testable import LookPauseMediaProbe

final class StatusIconTests: XCTestCase {
    func testAuthenticationAndWatchingOverrideOldDirection() {
        for intent in WatchingIntent.allCases {
            for watching in [false, true] {
                XCTAssertEqual(DesktopStatus(connected: false, watching: watching, intent: intent), .disconnected)
            }
            XCTAssertEqual(DesktopStatus(connected: true, watching: false, intent: intent), .waiting)
        }
        XCTAssertEqual(DesktopStatus(connected: true, watching: true, intent: .unknown), .waiting)
        XCTAssertEqual(DesktopStatus(connected: true, watching: true, intent: .attending), .attending)
        XCTAssertEqual(DesktopStatus(connected: true, watching: true, intent: .away), .away)
        XCTAssertEqual(DesktopStatus(connected: true, watching: true, intent: .noFace), .away)
    }

    @MainActor func testStartStopResetDirectionBeforeStatusNotification() {
        let media = MediaSession()
        var observed: [DesktopStatus] = []
        media.onStatus = { _ in
            observed.append(DesktopStatus(connected: true, watching: media.isWatching, intent: media.lastIntent))
        }
        media.start()
        media.receive(.attending)
        media.receive(.unknown)
        media.stop()
        media.receive(.attending)
        media.start()
        XCTAssertEqual(observed, [.waiting, .attending, .waiting, .waiting, .waiting])
        XCTAssertEqual(media.lastIntent, .unknown)
    }

    @MainActor func testFourShapesRenderInBothAppearances() throws {
        let preview = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: 720, pixelsHigh: 320,
            bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
            colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
        let oldContext = NSGraphicsContext.current
        defer { NSGraphicsContext.current = oldContext }
        NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: preview)
        let icons = DesktopStatus.allCases.map { $0.image() }
        var lightShapes: [Data] = []
        for (row, name) in [NSAppearance.Name.aqua, .darkAqua].enumerated() {
            let appearance = try XCTUnwrap(NSAppearance(named: name))
            appearance.performAsCurrentDrawingAppearance {
                (row == 0 ? NSColor.white : NSColor(srgbRed: 0.09, green: 0.10, blue: 0.12, alpha: 1)).setFill()
                NSRect(x: 0, y: CGFloat(1 - row) * 160, width: 720, height: 160).fill()
                var shapes = Set<Data>()
                for (column, state) in DesktopStatus.allCases.enumerated() {
                    let icon = icons[column]
                    XCTAssertEqual(icon.isTemplate, state == .disconnected)
                    guard let cg = icon.cgImage(forProposedRect: nil, context: nil, hints: nil),
                          let data = NSBitmapImageRep(cgImage: cg).representation(using: .png, properties: [:]) else {
                        XCTFail("图标无法渲染"); continue
                    }
                    shapes.insert(data)
                    if row == 0 { lightShapes.append(data) }
                    else { XCTAssertNotEqual(data, lightShapes[column], "复用同一图像切换主题仍须更新配色") }
                    let x = CGFloat(column) * 180 + 20, y = CGFloat(1 - row) * 160
                    icon.draw(in: NSRect(x: x, y: y + 85, width: 20, height: 20))
                    icon.draw(in: NSRect(x: x + 50, y: y + 55, width: 80, height: 80))
                    (state.label as NSString).draw(at: NSPoint(x: x, y: y + 20), withAttributes: [
                        .font: NSFont.systemFont(ofSize: 12), .foregroundColor: NSColor.labelColor])
                }
                XCTAssertEqual(shapes.count, 4, "图标必须在单色轮廓和填充上可区分")
                if let directory = ProcessInfo.processInfo.environment["PAUSEPAL_ICON_ASSETS"] {
                    for (column, state) in DesktopStatus.allCases.enumerated() {
                        for size in [16, 20, 24, 32, 40, 48, 64] {
                            let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: size, pixelsHigh: size,
                                bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
                            NSGraphicsContext.saveGraphicsState()
                            NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: bitmap)
                            icons[column].draw(in: NSRect(x: 0, y: 0, width: size, height: size))
                            NSGraphicsContext.restoreGraphicsState()
                            let file = URL(fileURLWithPath: directory).appendingPathComponent("\(row)-\(state.rawValue)-\(size).png")
                            do { try bitmap.representation(using: .png, properties: [:])!.write(to: file) }
                            catch { XCTFail("图标素材导出失败：\(error)") }
                        }
                    }
                }
            }
        }
        if let path = ProcessInfo.processInfo.environment["PAUSEPAL_ICON_PREVIEW"] {
            try XCTUnwrap(preview.representation(using: .png, properties: [:])).write(to: URL(fileURLWithPath: path))
        }
    }
}
