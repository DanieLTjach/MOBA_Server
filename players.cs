using System;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Players
{
    public class Player
    {

        public string id { get; set;}

        public Coordinates coordinates { get; set; }

        public Characteristics characteristics { get; set; }

        public bool team { get; set; }

        public Player(string id, Coordinates coordinates, Characteristics characteristics, bool team)
        {
            this.id = id;
            this.coordinates = coordinates;
            this.characteristics = characteristics;
            this.team = team;
        }
    }

    public class Coordinates{
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class Characteristics
    {
        public int Speed { get; set; }
        public int Attack { get; set; }
        public int Magic { get; set; }
        public double Defence { get; set; }
        public double Healthpoint { get; set; }
    }

}
namespace Characters
{
    public class BasicCharacter
    {
        public string Name {get; set;}

        public int Health { get; set; }
        public double HealthRegeneration {get; set;}
        public int Mana {get; set;}
        public double ManaRegeneration {get; set;}


        public int AttackDamage {get; set;}
        public int AttackSpeed {get; set;}
        public int AttackRange {get; set;}
        public int AbilityPower {get; set;}
        public double CriticalStrikeChance {get; set;}

        public int MoveSpeed {get; set;}

        public int LifeSteal {get; set;}
        public int SpeelVamp {get; set;}

        public int Armor {get; set;}
        public int MagicResist {get; set;}
        public int ArmorPenetration {get; set;}
        public int MagicPenetration {get; set;}

        public int Experience {get; set;}
        public int Level {get; set;}

        public bool Is_Q_Cooldown { get; set; }
        public bool Is_W_Cooldown { get; set; }
        public bool Is_E_Cooldown { get; set; }
        public bool Is_R_Cooldown { get; set; }

        public virtual int LevelUp(int experience, int level)
        {
            if(experience == 10*Math.Pow(level - 2, 2)+468){
                level++;
            }
            return level;
        }

        public virtual void Attack (int AttackRange, int AttackDamage, int AttackSpeed, int CriticalStrikeChance) 
        {
            Console.WriteLine($"attack.");
        }

        public virtual void TakeDamage(int Armor, int MagicPenetration, int MagicResist, int ArmorPenetration) 
        {
            Console.WriteLine($"take damage.");
        }

        public virtual void UseAbility()
        {
            Console.WriteLine($"{Name} uses a basic ability.");
        }

        public virtual void Heal(int amount, int time)
        {
            Console.WriteLine($"{Name} heals ");
        }

        public bool IsAlive()
        {
            return Health > 0;
        }

        public virtual void RegenerateResources()
        {
            Console.WriteLine($"regenerates resource.");
        }

        public virtual void ApplyBuff(string buffType, int duration)
        {
            Console.WriteLine($"buff.");
        }

        public virtual void ApplyDebuff(string debuffType, int duration)
        {
            Console.WriteLine($" debuff.");
        }

        public virtual void Move()
        {
            Console.WriteLine($"moves.");
        }
    }

    public class Sukuna : BasicCharacter
{

    public Sukuna()
    {
        Name = "Sukuna";
        Health = 550;
        HealthRegeneration = 0.5;
        Mana = 250;
        ManaRegeneration = 1;
        AttackDamage = 35;
        AttackSpeed = 250;
        AttackRange = 50;
        AbilityPower = 10;
        CriticalStrikeChance = 0;
        MoveSpeed = 1;
        Armor = 10;
        LifeSteal = 0;
        SpeelVamp = 0;
        MagicResist = 15;
        ArmorPenetration = 0;
        MagicPenetration = 0;
        Experience = 0;
        Level = 1;
        Is_Q_Cooldown = false;
        Is_W_Cooldown = false;
        Is_E_Cooldown = false;
        Is_R_Cooldown = false;
    }

    public override int LevelUp(int experience, int level)
    {
        int requiredExperience = 10 * (int)Math.Pow(level - 2, 2) + 468;
        if (experience >= requiredExperience)
        {
            level++;
            IncreaseStatsForLevel(level);  // Увеличиваем характеристики при повышении уровня
        }
        return level;
    }

    private void IncreaseStatsForLevel(int level)
    {
        Health += 50 + (level * 5);           
        HealthRegeneration += 0.1;            
        Mana += 20;                           
        ManaRegeneration += 0.05;             
        AttackDamage += 5 + (level / 2);      
        AttackSpeed += 10;                    
        Armor += 3;                           
        MagicResist += 3;                     

        Console.WriteLine($"{Name} leveled up to {level}! Stats increased. New stast: {Health} HP, {Mana} MP, {AttackDamage} AD, {Armor} Armor, {MagicResist} MR.");
    }

    public void Kokusen(int skill_level, int duration)
    {
        if (skill_level < 1)
        {
            return;
        }

        int bonusDamage = 50 + (skill_level * 10);
        AttackDamage += bonusDamage;

        Timer timer = new Timer();
        timer.Interval = duration * 1000;
        timer.Elapsed += (sender, e) =>
        {
            AttackDamage -= bonusDamage;
            Is_Q_Cooldown = true;
            timer.Stop();
            timer.Dispose();
            Console.WriteLine("Kokusen effect ended.");
        };
        timer.Start();

        Console.WriteLine($"Kokusen activated! Attack Damage increased by {bonusDamage} for {duration} seconds.");
    }

    public void KamiHi()
    {
        // Реализация способности
    }

    public void HantenJutsushiki()
    {
        // Реализация способности
    }

    public void FukumaMizushi()
    {
        // Реализация способности
    }
}

    public class AyaseMomo : BasicCharacter
    {
        public AyaseMomo()
        {
            Name = "AyaseMomo";
            Health = 350;
            HealthRegeneration = 1;
            Mana = 200;
            ManaRegeneration = 1;
            AttackDamage = 25;
            AttackSpeed = 250;
            AttackRange = 300;
            AbilityPower = 10;
            CriticalStrikeChance = 0;
            MoveSpeed = 1;
            Armor = 20;
            LifeSteal = 0;
            SpeelVamp = 0;
            MagicResist = 20;
            ArmorPenetration = 0;
            MagicPenetration = 0;
            Experience = 0;
            Level = 1;
            Is_Q_Cooldown = false;
            Is_W_Cooldown = false;
            Is_E_Cooldown = false;
            Is_R_Cooldown = false;
        }



        public void ORakendasu(){ // Обнаружение Ауры (Q)
        
        }
        
        public void Omouchikara(){ // Психокинез (W)
        
        }

        public void MoeTriBeam(){ // Направленный психический взрыв (E)
            
        }
        
        public void Okaruun(){ // Призыв Окаруна (R)
            
        }

        /* public void OkarunQ(){ // ??? (Q)
            
        }
        public void OkarunW(){ // ??? (W)
            
        }
        public void OkarunE(){ // ??? (E)
            
        }
        public void OkarunR(){ // Превращение обратно (R)
            
        }
        */
        
    }

    public class Denji : BasicCharacter
    {
        public Denji()
        {
            
        }
        /*
        public void PikaPika(){ // Пика-Пика (Q)
            
        }
        
        public void PikaPikaPika(){ // Пика-Пика-Пика (W)
            
        }
        
        public void PikaPikaPikaPika(){ // Пика-Пика-Пика-Пика (E)
            
        }
        
        public void PikaPikaPikaPikaPika(){ // Пика-Пика-Пика-Пика-Пика (R)
            
        }
        */
    }
}
