import AVFoundation
import Foundation

// AVSpeechSynthesizer behind ITextToSpeech. Each utterance completes its C# call through its token: 1 when it was
// spoken, 0 when it was stopped.
final class WatchiousSpeech: NSObject, AVSpeechSynthesizerDelegate, @unchecked Sendable {
    static let shared = WatchiousSpeech()

    private let synthesizer = AVSpeechSynthesizer()
    private let lock = NSLock()
    private var tokens: [ObjectIdentifier: Int64] = [:]

    override init() {
        super.init()
        synthesizer.delegate = self
    }

    // A negative pitch or volume keeps the voice's own; pitch goes from 0 to 2, as in C#, and AVSpeechUtterance
    // takes 0.5 to 2.
    func speak(_ text: String, language: String?, pitch: Float, volume: Float, token: Int64) {
        let utterance = AVSpeechUtterance(string: text)
        if let language, !language.isEmpty {
            utterance.voice = AVSpeechSynthesisVoice(language: language)
        }
        if pitch >= 0 {
            utterance.pitchMultiplier = max(0.5, min(2, pitch))
        }
        if volume >= 0 {
            utterance.volume = max(0, min(1, volume))
        }
        lock.lock()
        tokens[ObjectIdentifier(utterance)] = token
        lock.unlock()
        synthesizer.speak(utterance)
    }

    func stop() {
        synthesizer.stopSpeaking(at: .immediate)
    }

    func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didFinish utterance: AVSpeechUtterance) {
        complete(utterance, 1)
    }

    func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didCancel utterance: AVSpeechUtterance) {
        complete(utterance, 0)
    }

    private func complete(_ utterance: AVSpeechUtterance, _ result: Int32) {
        lock.lock()
        let token = tokens.removeValue(forKey: ObjectIdentifier(utterance))
        lock.unlock()
        if let token {
            WatchiousEssentialsCallbacks.shared.complete(token, result)
        }
    }
}

@_cdecl("watchious_speech_speak")
public func watchiousSpeechSpeak(
    _ text: UnsafePointer<CChar>,
    _ language: UnsafePointer<CChar>?,
    _ pitch: Float,
    _ volume: Float,
    _ token: Int64
) {
    WatchiousSpeech.shared.speak(
        String(cString: text),
        language: language.map { String(cString: $0) },
        pitch: pitch,
        volume: volume,
        token: token)
}

@_cdecl("watchious_speech_stop")
public func watchiousSpeechStop() {
    WatchiousSpeech.shared.stop()
}

// The installed voices, one per line as "language\tname\tidentifier", from strdup, which C# frees.
@_cdecl("watchious_speech_voices")
public func watchiousSpeechVoices() -> UnsafeMutablePointer<CChar>? {
    let lines = AVSpeechSynthesisVoice.speechVoices().map { voice in
        "\(voice.language)\t\(voice.name)\t\(voice.identifier)"
    }
    return strdup(lines.joined(separator: "\n"))
}
